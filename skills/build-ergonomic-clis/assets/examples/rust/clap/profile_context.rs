use std::collections::BTreeMap;
use std::env;
use std::time::Duration;

use std::fmt;
use url::Url;

// This example implements the **hostname-key** target identity mode (the Jellyfin worked-example choice).
// - Network operations use a normalized base URL (scheme/port/path may matter).
// - Credentials and defaults bind to the hostname identity key (lowercased hostname).

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum OutputFormat {
    Table,
    Json,
}

impl Default for OutputFormat {
    fn default() -> Self {
        Self::Table
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum AuthSource {
    None,
    Flag,
    Environment,
    Profile,
}

#[derive(Debug, Default, Clone)]
pub struct GlobalOpts {
    pub host: Option<String>,
    pub profile: Option<String>,
    pub token: Option<String>,
    pub json: bool,
    pub output: Option<OutputFormat>,
    pub timeout: Option<Duration>,
    pub retries: Option<u32>,
}

#[derive(Debug, Default, Clone)]
pub struct Config {
    pub active_profile: Option<String>,
    pub profiles: BTreeMap<String, ProfileConfig>,
    pub target_defaults: BTreeMap<String, String>,
}

#[derive(Debug, Default, Clone)]
pub struct ProfileConfig {
    pub hostname: Option<String>,
    pub base_url: Option<String>,
    pub output: Option<OutputFormat>,
    pub timeout: Option<Duration>,
    pub retries: Option<u32>,
}

#[derive(Debug, Clone)]
pub struct EffectiveConfig {
    pub profile: String,
    pub base_url: String,
    pub target_identity_key: String,
    pub token: Option<String>,
    pub output: OutputFormat,
    pub timeout: Duration,
    pub retries: u32,
    pub auth_source: AuthSource,
}

#[derive(Debug, Clone)]
pub struct CliError {
    pub exit_code: i32,
    pub message: String,
}

impl fmt::Display for CliError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.message)
    }
}

impl std::error::Error for CliError {}

impl CliError {
    pub fn usage(message: impl Into<String>) -> Self {
        Self {
            exit_code: 2,
            message: message.into(),
        }
    }
}

pub trait CredentialStore {
    fn get_token(&self, target_identity_key: &str, profile: &str) -> Option<String>;
}

pub fn resolve_effective_config(
    global: &GlobalOpts,
    config: &Config,
    credentials: &dyn CredentialStore,
) -> Result<EffectiveConfig, CliError> {
    let explicit_profile = first_non_empty([
        global.profile.clone(),
        env::var("TOOL_PROFILE").ok(),
    ]);

    let explicit_base_url = first_non_empty([
        global.host.clone(),
        env::var("TOOL_HOST").ok(),
    ])
    .map(|raw| normalize_base_url_input(&raw))
    .transpose()?;

    let explicit_target_key = explicit_base_url
        .as_deref()
        .map(target_identity_hostname_key)
        .transpose()?;

    let profile = match explicit_profile.clone() {
        Some(profile) => profile,
        None => match explicit_target_key.as_deref() {
            Some(target_key) => {
                if let Some(selected) = select_profile_for_target(target_key, config)? {
                    selected
                } else if let Some(active) = config.active_profile.clone() {
                    let active_cfg = config.profiles.get(&active).cloned().unwrap_or_default();
                    let active_target_key = if let Some(hostname) = active_cfg.hostname.as_deref() {
                        Some(normalize_hostname(hostname)?)
                    } else if let Some(base_url) = active_cfg.base_url.as_deref() {
                        Some(target_identity_hostname_key(&normalize_base_url_input(base_url)?)?)
                    } else {
                        None
                    };

                    if active_target_key.as_deref() == Some(target_key) {
                        active
                    } else {
                        "default".to_string()
                    }
                } else {
                    "default".to_string()
                }
            }
            None => config
                .active_profile
                .clone()
                .unwrap_or_else(|| "default".to_string()),
        },
    };

    let profile_cfg = config.profiles.get(&profile).cloned().unwrap_or_default();

    let profile_target_key = if let Some(hostname) = profile_cfg.hostname.as_deref() {
        Some(normalize_hostname(hostname)?)
    } else if let Some(base_url) = profile_cfg.base_url.as_deref() {
        Some(target_identity_hostname_key(&normalize_base_url_input(base_url)?)?)
    } else {
        None
    };

    if explicit_profile.is_some() {
        if let (Some(explicit_target_key), Some(profile_target_key)) =
            (explicit_target_key.as_deref(), profile_target_key.as_deref())
        {
            if explicit_target_key != profile_target_key {
                return Err(CliError::usage(format!(
                    "profile '{profile}' is configured for '{profile_target_key}', but the target is '{explicit_target_key}'"
                )));
            }
        }
    }

    let base_url = match explicit_base_url {
        Some(url) => url,
        None => match profile_cfg.base_url.as_deref() {
            Some(url) => normalize_base_url_input(url)?,
            None => {
                return Err(CliError::usage(
                    "unable to resolve target base url. Pass --host/TOOL_HOST or configure a profile base_url."
                        .to_string(),
                ))
            }
        },
    };

    let target_identity_key = target_identity_hostname_key(&base_url)?;

    let token_from_flag = first_non_empty([global.token.clone()]);
    let token_from_env = first_non_empty([env::var("TOOL_TOKEN").ok()]);
    let token = token_from_flag
        .clone()
        .or(token_from_env.clone())
        .or_else(|| credentials.get_token(&target_identity_key, &profile));

    let auth_source = if token_from_flag.is_some() {
        AuthSource::Flag
    } else if token_from_env.is_some() {
        AuthSource::Environment
    } else if token.is_some() {
        AuthSource::Profile
    } else {
        AuthSource::None
    };

    let output = if global.json {
        OutputFormat::Json
    } else {
        global.output.or(profile_cfg.output).unwrap_or(OutputFormat::Table)
    };

    Ok(EffectiveConfig {
        profile,
        base_url,
        target_identity_key,
        token,
        output,
        timeout: global
            .timeout
            .or(profile_cfg.timeout)
            .unwrap_or_else(|| Duration::from_secs(30)),
        retries: global.retries.or(profile_cfg.retries).unwrap_or(3),
        auth_source,
    })
}

fn select_profile_for_target(target_key: &str, config: &Config) -> Result<Option<String>, CliError> {
    if let Some(profile) = config.target_defaults.get(target_key) {
        if !config.profiles.contains_key(profile) {
            return Err(CliError::usage(format!(
                "target default for '{target_key}' references missing profile '{profile}'"
            )));
        }
        return Ok(Some(profile.clone()));
    }

    // Be forgiving if the config keys were not pre-normalized (e.g. mixed casing or URL-ish keys).
    let mut normalized_matches = config
        .target_defaults
        .iter()
        .filter_map(|(key, profile)| {
            let normalized_key = normalize_hostname(key).ok()?;
            if normalized_key == target_key {
                Some(profile)
            } else {
                None
            }
        })
        .collect::<Vec<_>>();

    normalized_matches.sort();
    normalized_matches.dedup();

    if normalized_matches.len() == 1 {
        let profile = normalized_matches[0];
        if !config.profiles.contains_key(profile) {
            return Err(CliError::usage(format!(
                "target default for '{target_key}' references missing profile '{profile}'"
            )));
        }
        return Ok(Some(profile.clone()));
    } else if normalized_matches.len() > 1 {
        return Err(CliError::usage(format!(
            "multiple target defaults match '{target_key}'. Normalize the keys or pass --profile."
        )));
    }

    let matching_profiles = config
        .profiles
        .iter()
        .filter_map(|(name, profile)| {
            let key = if let Some(hostname) = profile.hostname.as_deref() {
                normalize_hostname(hostname).ok()
            } else if let Some(base_url) = profile.base_url.as_deref() {
                normalize_base_url_input(base_url)
                    .ok()
                    .and_then(|url| target_identity_hostname_key(&url).ok())
            } else {
                None
            }?;

            if key == target_key {
                Some(name.clone())
            } else {
                None
            }
        })
        .collect::<Vec<_>>();

    match matching_profiles.len() {
        0 => Ok(None),
        1 => Ok(matching_profiles.into_iter().next()),
        _ => Err(CliError::usage(format!(
            "multiple profiles match '{target_key}'. Pass --profile or define a target default."
        ))),
    }
}

pub fn normalize_base_url_input(raw: &str) -> Result<String, CliError> {
    let trimmed = raw.trim();
    let normalized = if trimmed.contains("://") {
        trimmed.to_string()
    } else if trimmed.contains(':') && trimmed.parse::<std::net::Ipv6Addr>().is_ok() {
        format!("https://[{trimmed}]")
    } else {
        format!("https://{trimmed}")
    };

    let url = Url::parse(&normalized)
        .map_err(|err| CliError::usage(format!("invalid target url '{raw}': {err}")))?;

    let scheme = url.scheme().to_ascii_lowercase();
    let host = url
        .host_str()
        .ok_or_else(|| CliError::usage(format!("invalid target url '{raw}': missing hostname")))?;

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

    let path = url.path().trim_end_matches('/');
    let path = if path.is_empty() || path == "/" { "" } else { path };

    Ok(format!("{scheme}://{host}{port}{path}"))
}

pub fn normalize_hostname(raw: &str) -> Result<String, CliError> {
    let trimmed = raw.trim();
    if trimmed.is_empty() {
        return Err(CliError::usage("hostname is required".to_string()));
    }

    if !trimmed.contains("://")
        && !trimmed.contains('/')
        && !trimmed.contains('?')
        && !trimmed.contains('#')
        && trimmed.contains(':')
        && trimmed.parse::<std::net::Ipv6Addr>().is_ok()
    {
        // Allow bare IPv6 hostnames without brackets (identity keys are host-only).
        return Ok(trimmed.to_ascii_lowercase());
    }

    let looks_like_windows_path = trimmed.starts_with(r"\\")
        || (trimmed.len() >= 3
            && trimmed.as_bytes()[1] == b':'
            && (trimmed.as_bytes()[2] == b'\\' || trimmed.as_bytes()[2] == b'/'));
    if looks_like_windows_path {
        return Err(CliError::usage(format!("invalid hostname '{raw}'")));
    }

    let looks_like_url = trimmed.contains("://")
        || trimmed.contains('/')
        || trimmed.contains(':')
        || trimmed.contains('?')
        || trimmed.contains('#');
    if looks_like_url {
        let candidate = if trimmed.contains("://") {
            trimmed.to_string()
        } else {
            format!("https://{trimmed}")
        };

        match Url::parse(&candidate) {
            Ok(url) => {
                let host = url
                    .host_str()
                    .ok_or_else(|| CliError::usage(format!("invalid hostname '{raw}': missing hostname")))?;
                return Ok(host.trim_matches(&['[', ']'][..]).to_ascii_lowercase());
            }
            Err(_) => {
                if trimmed.contains("://") {
                    return Err(CliError::usage(format!("invalid hostname '{raw}'")));
                }

                if trimmed.contains(':') && !trimmed.contains('/') && !trimmed.contains('@') {
                    // Looks like a host:port input, but did not parse as a URL (likely invalid port).
                    return Err(CliError::usage(format!("invalid hostname '{raw}'")));
                }

                let without_scheme = trimmed.splitn(2, "://").nth(1).unwrap_or(trimmed);
                let without_userinfo = without_scheme.rsplit_once('@').map(|(_, rest)| rest).unwrap_or(without_scheme);
                let authority = without_userinfo
                    .split(|c| c == '/' || c == '?' || c == '#')
                    .next()
                    .unwrap_or(without_userinfo)
                    .trim();

                let host = if authority.starts_with('[') {
                    let end = authority
                        .find(']')
                        .ok_or_else(|| CliError::usage(format!("invalid hostname '{raw}'")))?;
                    authority[1..end].trim()
                } else {
                    // If the input looks like scheme-less host:port[/...] but URL parsing failed above,
                    // reject it to avoid silently collapsing to just the host.
                    let looks_like_host_port = match authority.split_once(':') {
                        Some((_, rest)) => {
                            let port = rest.split_once('/').map(|(p, _)| p).unwrap_or(rest);
                            !port.is_empty()
                        }
                        None => false,
                    };
                    if looks_like_host_port {
                        return Err(CliError::usage(format!("invalid hostname '{raw}'")));
                    }
                    authority.split_once(':').map(|(h, _)| h.trim()).unwrap_or(authority)
                };

                if host.is_empty() {
                    return Err(CliError::usage(format!("invalid hostname '{raw}'")));
                }

                return Ok(host.to_ascii_lowercase());
            }
        }
    }

    Ok(trimmed.to_ascii_lowercase())
}

pub fn target_identity_hostname_key(normalized_base_url: &str) -> Result<String, CliError> {
    let url = Url::parse(normalized_base_url)
        .map_err(|err| CliError::usage(format!("invalid base url '{normalized_base_url}': {err}")))?;
    let host = url
        .host_str()
        .ok_or_else(|| CliError::usage(format!("invalid base url '{normalized_base_url}': missing hostname")))?;
    normalize_hostname(host)
}

fn first_non_empty<const N: usize>(candidates: [Option<String>; N]) -> Option<String> {
    candidates
        .iter()
        .filter_map(|value| value.as_ref())
        .find(|value| !value.trim().is_empty())
        .cloned()
}
