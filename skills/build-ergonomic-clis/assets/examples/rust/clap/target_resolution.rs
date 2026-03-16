use url::Url;

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
        if let Some(remote) = select_remote(remotes, args.remote.as_deref(), args.host.as_deref())? {
            if let Some((host, repo)) = remote_url_to_host_and_repo(&remote.url)? {
                resolved_base_url.get_or_insert(normalize_base_url(&host)?);
                resolved_repo.get_or_insert(repo);
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
    let (head, name) = raw
        .rsplit_once('/')
        .ok_or_else(|| CliError("repo must be [HOST/]OWNER/NAME".to_string()))?;
    let name = name.strip_suffix(".git").unwrap_or(name);
    let (host, owner) = match head.rsplit_once('/') {
        Some((host, owner)) => (Some(host.to_string()), owner.to_string()),
        None => (None, head.to_string()),
    };

    Ok(RepoArg {
        host,
        owner,
        name: name.to_string(),
    })
}

pub fn normalize_base_url(raw: &str) -> Result<String, CliError> {
    let trimmed = raw.trim();
    if trimmed.is_empty() {
        return Err(CliError("host is required".to_string()));
    }

    let candidate = if trimmed.contains("://") {
        trimmed.to_string()
    } else {
        format!("https://{trimmed}")
    };

    let url = Url::parse(&candidate).map_err(|err| CliError(format!("invalid host: {err}")))?;
    let host = url
        .host_str()
        .ok_or_else(|| CliError("url is missing host".to_string()))?;

    let port = url.port().map(|port| format!(":{port}")).unwrap_or_default();
    Ok(format!("{}://{}{}", url.scheme(), host, port))
}

pub fn normalize_host_key(raw: &str) -> Result<String, CliError> {
    let base = normalize_base_url(raw)?;
    let url = Url::parse(&base).map_err(|err| CliError(format!("invalid host: {err}")))?;
    Ok(match url.port() {
        Some(port) => format!("{}:{port}", url.host_str().unwrap_or_default()),
        None => url.host_str().unwrap_or_default().to_string(),
    })
}

fn select_remote<'a>(
    remotes: &'a [GitRemote],
    preferred: Option<&str>,
    host_hint: Option<&str>,
) -> Result<Option<&'a GitRemote>, CliError> {
    if let Some(name) = preferred {
        return Ok(remotes.iter().find(|remote| remote.name == name));
    }

    if remotes.len() == 1 {
        return Ok(remotes.first());
    }

    if let Some(remote) = remotes.iter().find(|remote| remote.tracks_head) {
        return Ok(Some(remote));
    }

    if let Some(host_hint) = host_hint {
        let host_key = normalize_host_key(host_hint)?;
        if let Some(remote) = remotes.iter().find(|remote| {
            remote_url_to_host_and_repo(&remote.url)
                .ok()
                .flatten()
                .map(|(host, _)| normalize_host_key(&host).ok().as_deref() == Some(host_key.as_str()))
                .unwrap_or(false)
        }) {
            return Ok(Some(remote));
        }
    }

    if let Some(origin) = remotes.iter().find(|remote| remote.name == "origin") {
        return Ok(Some(origin));
    }

    Ok(remotes.first())
}

fn remote_url_to_host_and_repo(raw: &str) -> Result<Option<(String, String)>, CliError> {
    let url = parse_remote_url(raw)?;
    let host = url
        .host_str()
        .ok_or_else(|| CliError("remote url missing host".to_string()))?;

    let mut segments = url
        .path_segments()
        .ok_or_else(|| CliError("remote url cannot be a base".to_string()))?
        .filter(|segment| !segment.is_empty())
        .collect::<Vec<_>>();

    if segments.len() < 2 {
        return Ok(None);
    }

    let name = segments.pop().unwrap().trim_end_matches(".git");
    let owner = segments.pop().unwrap();
    Ok(Some((host.to_string(), format!("{owner}/{name}"))))
}

fn parse_remote_url(raw: &str) -> Result<Url, CliError> {
    if let Ok(url) = Url::parse(raw) {
        return Ok(url);
    }

    let at_index = raw
        .find('@')
        .ok_or_else(|| CliError(format!("unable to parse remote url '{raw}'")))?;
    let mut rewritten = String::from("ssh://");
    rewritten.push_str(&raw[..at_index]);
    rewritten.push_str(&raw[at_index..].replacen(':', "/", 1));

    Url::parse(&rewritten).map_err(|err| CliError(format!("unable to parse remote url '{raw}': {err}")))
}
