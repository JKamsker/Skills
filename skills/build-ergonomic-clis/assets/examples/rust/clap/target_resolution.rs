use std::fmt;

use url::Url;

// This example shows *context inference* for a git-like CLI.
//
// Design choices in this sketch:
// - Network operations use a normalized base URL (scheme + host + non-default port), dropping path/query/fragment.
// - Remote inference yields a host hint; for http(s) remotes it preserves scheme + port, otherwise it yields hostname only.
// - Identity comparisons for selecting a git remote use a hostname-key (lowercased hostname; IPv6 without brackets), not an origin-key.

#[derive(Debug, Clone)]
pub struct RepoArg {
    pub host: Option<String>,
    pub owner: String,
    pub name: String,
}

#[derive(Debug, Clone)]
pub struct TargetArgs {
    pub host: Option<String>,
    pub repo: Option<RepoArg>,
    pub remote: Option<String>,
}

#[derive(Debug, Clone)]
pub struct GitRemote {
    pub name: String,
    pub url: String,
    pub tracks_head: bool,
}

#[derive(Debug, Clone)]
pub struct ResolvedTarget {
    pub base_url: String,
    pub repo: Option<String>,
}

#[derive(Debug, Clone)]
pub struct CliError(pub String);

impl fmt::Display for CliError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.0)
    }
}

impl std::error::Error for CliError {}

pub fn resolve_target(
    args: &TargetArgs,
    remotes: &[GitRemote],
    env_fallback_host: Option<&str>,
) -> Result<ResolvedTarget, CliError> {
    let mut resolved_repo = args
        .repo
        .as_ref()
        .map(|repo| format!("{}/{}", repo.owner, repo.name));

    let mut resolved_base_url = args
        .repo
        .as_ref()
        .and_then(|repo| repo.host.as_deref())
        .map(normalize_base_url)
        .transpose()?;

    if resolved_base_url.is_none() {
        resolved_base_url = args.host.as_deref().map(normalize_base_url).transpose()?;
    }

    if resolved_base_url.is_none() || resolved_repo.is_none() {
        let host_hint = if args.host.is_some() || args.repo.as_ref().and_then(|repo| repo.host.as_deref()).is_some() {
            resolved_base_url.as_deref().or(args.host.as_deref())
        } else {
            None
        };
        if let Some(remote) = select_remote(remotes, args.remote.as_deref(), host_hint)? {
            let needs_inference = resolved_base_url.is_none() || resolved_repo.is_none();
            match remote_url_to_host_and_repo(&remote.url) {
                Ok(Some((host, repo))) => {
                    resolved_base_url.get_or_insert(normalize_base_url(&host)?);
                    if let Some(repo) = repo {
                        resolved_repo.get_or_insert(repo);
                    }
                }
                Ok(None) => {
                    if args.remote.is_some() && needs_inference {
                        return Err(CliError(format!(
                            "remote '{}' does not contain a host; pass --host/--repo explicitly",
                            remote.name
                        )));
                    }
                }
                Err(err) => {
                    if args.remote.is_some() && needs_inference {
                        return Err(CliError(format!(
                            "{err}. Pass --host/--repo explicitly or choose a different remote."
                        )));
                    }
                }
            }
        }
    }

    if resolved_base_url.is_none() {
        resolved_base_url = env_fallback_host.map(normalize_base_url).transpose()?;
    }

    let base_url = resolved_base_url.ok_or_else(|| {
        CliError(
            "unable to resolve host. Pass --host, embed the host in --repo, provide a matching git remote, or set a fallback host.".to_string(),
        )
    })?;

    Ok(ResolvedTarget {
        base_url,
        repo: resolved_repo,
    })
}

pub fn parse_repo_arg(raw: &str) -> Result<RepoArg, CliError> {
    let raw = raw.trim();
    let segments = raw.split('/').filter(|s| !s.is_empty()).collect::<Vec<_>>();
    if segments.len() < 2 {
        return Err(CliError("repo must be [HOST/]OWNER[/...]/NAME".to_string()));
    }

    let last = segments[segments.len() - 1];
    let name = last.strip_suffix(".git").unwrap_or(last).to_string();

    let first = segments[0];
    let looks_like_host = first.contains('.')
        || first.contains(':')
        || first.eq_ignore_ascii_case("localhost")
        || first.parse::<std::net::IpAddr>().is_ok();

    let (host, owner_segments) = if segments.len() >= 3 && looks_like_host {
        (Some(first.to_string()), &segments[1..segments.len() - 1])
    } else {
        (None, &segments[..segments.len() - 1])
    };

    let owner = owner_segments.join("/");

    Ok(RepoArg {
        host,
        owner,
        name,
    })
}

pub fn normalize_base_url(raw: &str) -> Result<String, CliError> {
    let trimmed = raw.trim();
    if trimmed.is_empty() {
        return Err(CliError("host is required".to_string()));
    }

    let candidate = if trimmed.contains("://") {
        trimmed.to_string()
    } else if trimmed.contains(':') && trimmed.parse::<std::net::Ipv6Addr>().is_ok() {
        format!("https://[{trimmed}]")
    } else {
        format!("https://{trimmed}")
    };

    let url = Url::parse(&candidate)
        .map_err(|err| CliError(format!("invalid host '{raw}': {err}")))?;
    let host = url
        .host_str()
        .ok_or_else(|| CliError(format!("invalid host '{raw}': missing hostname")))?;

    let scheme = url.scheme().to_ascii_lowercase();
    let default_port = match scheme.as_str() {
        "http" => Some(80),
        "https" => Some(443),
        _ => None,
    };

    let port = match (url.port(), default_port) {
        (Some(port), Some(default_port)) if port != default_port => format!(":{port}"),
        (Some(port), None) => format!(":{port}"),
        _ => String::new(),
    };

    Ok(format!("{scheme}://{host}{port}"))
}

pub fn hostname_identity_key(raw: &str) -> Result<String, CliError> {
    let base = normalize_base_url(raw)?;
    let url = Url::parse(&base)
        .map_err(|err| CliError(format!("invalid base url '{base}': {err}")))?;
    let host = url
        .host_str()
        .ok_or_else(|| CliError(format!("invalid base url '{base}': missing hostname")))?;
    Ok(host.trim_matches(&['[', ']'][..]).to_ascii_lowercase())
}

fn select_remote<'a>(
    remotes: &'a [GitRemote],
    preferred: Option<&str>,
    host_hint: Option<&str>,
) -> Result<Option<&'a GitRemote>, CliError> {
    if let Some(name) = preferred {
        return remotes
            .iter()
            .find(|remote| remote.name == name)
            .map(Some)
            .ok_or_else(|| CliError(format!("unknown remote '{name}'")));
    }

    if let Some(host_hint) = host_hint {
        let host_key = hostname_identity_key(host_hint)?;
        if let Some(remote) = remotes.iter().find(|remote| {
            remote_url_to_host_and_repo(&remote.url)
                .ok()
                .flatten()
                .map(|(host, _)| hostname_identity_key(&host).ok().as_deref() == Some(host_key.as_str()))
                .unwrap_or(false)
        }) {
            return Ok(Some(remote));
        }
        return Ok(None);
    }

    if remotes.len() == 1 {
        return Ok(remotes.first());
    }

    if let Some(remote) = remotes.iter().find(|remote| remote.tracks_head) {
        return Ok(Some(remote));
    }

    if let Some(origin) = remotes.iter().find(|remote| remote.name == "origin") {
        return Ok(Some(origin));
    }

    Ok(remotes.first())
}

fn remote_url_to_host_and_repo(raw: &str) -> Result<Option<(String, Option<String>)>, CliError> {
    let trimmed = raw.trim();
    let looks_like_windows_path = trimmed.starts_with(r"\\")
        || (trimmed.len() >= 3
            && trimmed.as_bytes()[1] == b':'
            && (trimmed.as_bytes()[2] == b'\\' || trimmed.as_bytes()[2] == b'/'));
    let looks_like_drive_relative = trimmed.len() >= 2
        && trimmed.as_bytes()[1] == b':'
        && !trimmed.contains("://")
        && !trimmed.contains('/')
        && !trimmed.contains('\\');
    let looks_like_windows_relative = trimmed.starts_with(".\\")
        || trimmed.starts_with("..\\")
        || trimmed.starts_with("~\\")
        || trimmed.starts_with(".//")
        || trimmed.starts_with("..//")
        || trimmed.starts_with("~/");
    let looks_like_unix_path = trimmed.starts_with('/')
        || trimmed.starts_with("./")
        || trimmed.starts_with("../")
        || trimmed.starts_with("~/");
    let looks_like_file_url = trimmed.starts_with("file://");
    if looks_like_windows_path
        || looks_like_drive_relative
        || looks_like_windows_relative
        || looks_like_unix_path
        || looks_like_file_url
        || trimmed.starts_with("gitdir:")
    {
        return Ok(None);
    }

    let url = parse_remote_url(raw)?;
    let host = url
        .host_str()
        .ok_or_else(|| CliError(format!("unable to parse remote url '{raw}': missing hostname")))?;

    let host_hint = match url.scheme() {
        "http" | "https" => {
            let scheme = url.scheme().to_ascii_lowercase();
            let port = url.port().map(|port| format!(":{port}")).unwrap_or_default();
            format!("{scheme}://{host}{port}")
        }
        _ => host.to_string(),
    };

    let looks_like_sshy_path = !trimmed.contains("://")
        && (trimmed.contains(":/") || trimmed.contains(":~/") || trimmed.contains(":~\\"));
    let looks_like_absolute_ssh_path = url.scheme() == "ssh" && url.path().starts_with("//");

    let mut segments = url
        .path_segments()
        .ok_or_else(|| CliError(format!("unable to parse remote url '{raw}': cannot be a base url")))?
        .filter(|segment| !segment.is_empty())
        .collect::<Vec<_>>();

    let repo = match segments.len() {
        0 => None,
        1 => None,
        _ => {
            let name = segments.pop().unwrap();
            let name = name.strip_suffix(".git").unwrap_or(name);
            let owner = segments.join("/");
            if url.scheme() == "ssh"
                && (looks_like_sshy_path || looks_like_absolute_ssh_path || owner.starts_with('~') || owner.starts_with('/'))
            {
                None
            } else {
                Some(format!("{owner}/{name}"))
            }
        }
    };

    Ok(Some((host_hint, repo)))
}

fn parse_remote_url(raw: &str) -> Result<Url, CliError> {
    match Url::parse(raw) {
        Ok(url) => {
            if url.host_str().is_some() {
                return Ok(url);
            }
            if raw.contains("://") {
                return Err(CliError(format!(
                    "unable to parse remote url '{raw}': missing hostname"
                )));
            }
        }
        Err(err) => {
            if raw.contains("://") {
                return Err(CliError(format!("unable to parse remote url '{raw}': {err}")));
            }
        }
    }

    let trimmed = raw.trim();
    let looks_like_windows_path = trimmed.starts_with(r"\\")
        || (trimmed.len() >= 3
            && trimmed.as_bytes()[1] == b':'
            && (trimmed.as_bytes()[2] == b'\\' || trimmed.as_bytes()[2] == b'/'));
    if looks_like_windows_path {
        return Err(CliError(format!("unable to parse remote url '{raw}'")));
    }

    let (host_with_user, path) =
        split_scp_host_and_path(trimmed).map_err(|err| CliError(format!("unable to parse remote url '{raw}': {err}")))?;

    let (user_prefix, host) = match host_with_user.rsplit_once('@') {
        Some((user, host)) if !user.is_empty() && !host.is_empty() => (format!("{user}@"), host),
        Some(_) => {
            return Err(CliError(format!(
                "unable to parse remote url '{raw}': invalid scp-style remote; expected [user@]host:path"
            )));
        }
        None => (String::new(), host_with_user),
    };
    let host = if !host.starts_with('[') && host.parse::<std::net::Ipv6Addr>().is_ok() {
        format!("[{host}]")
    } else {
        host.to_string()
    };

    let mut rewritten = String::from("ssh://");
    rewritten.push_str(&user_prefix);
    rewritten.push_str(&host);
    rewritten.push('/');
    rewritten.push_str(path);

    Url::parse(&rewritten).map_err(|err| CliError(format!("unable to parse remote url '{raw}': {err}")))
}

fn split_scp_host_and_path(input: &str) -> Result<(&str, &str), CliError> {
    let trimmed = input.trim();
    if trimmed.is_empty() {
        return Err(CliError("empty".to_string()));
    }

    let mut in_brackets = false;
    let prefix_end = trimmed.find('/').unwrap_or(trimmed.len());
    let prefix = &trimmed[..prefix_end];
    let mut colon_positions = Vec::new();
    for (index, ch) in trimmed.char_indices() {
        match ch {
            '[' => in_brackets = true,
            ']' => in_brackets = false,
            ':' if !in_brackets => {
                colon_positions.push(index);
                if index >= prefix_end {
                    break;
                }
            }
            _ => {}
        }
    }

    if in_brackets {
        return Err(CliError(
            "invalid scp-style remote; expected [user@][ipv6]:path".to_string(),
        ));
    }

    let sep_index = *colon_positions
        .first()
        .ok_or_else(|| CliError("invalid scp-style remote; expected [user@]host:path".to_string()))?;

    if let Some(last_prefix_colon) = colon_positions.iter().copied().filter(|index| *index < prefix_end).last() {
        if last_prefix_colon != sep_index {
            let ipv6_candidate = prefix[..last_prefix_colon]
                .rsplit_once('@')
                .map(|(_, host)| host)
                .unwrap_or(&prefix[..last_prefix_colon]);
            if !ipv6_candidate.starts_with('[') && ipv6_candidate.parse::<std::net::Ipv6Addr>().is_ok() {
                return Err(CliError(
                    "invalid scp-style remote; unbracketed IPv6 hosts are ambiguous; use [user@][ipv6]:path"
                        .to_string(),
                ));
            }
        }
    }

    let host = trimmed[..sep_index].trim();
    let path = trimmed[sep_index + 1..].trim();

    if host.is_empty() || path.is_empty() {
        return Err(CliError(
            "invalid scp-style remote; expected [user@]host:path".to_string(),
        ));
    }

    Ok((host, path))
}
