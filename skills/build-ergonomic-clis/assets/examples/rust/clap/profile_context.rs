use std::collections::BTreeMap;
use std::env;
use std::time::Duration;

use url::Url;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub enum OutputFormat {
    #[default]
    Table,
    Json,
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
    pub host_defaults: BTreeMap<String, String>,
}

#[derive(Debug, Default, Clone)]
pub struct ProfileConfig {
    pub host: Option<String>,
    pub token: Option<String>,
    pub output: Option<OutputFormat>,
    pub timeout: Option<Duration>,
    pub retries: Option<u32>,
}

#[derive(Debug, Clone)]
pub struct EffectiveConfig {
    pub profile: String,
    pub host: String,
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

impl CliError {
    pub fn usage(message: impl Into<String>) -> Self {
        Self {
            exit_code: 2,
            message: message.into(),
        }
    }
}

pub fn resolve_effective_config(global: &GlobalOpts, config: &Config) -> Result<EffectiveConfig, CliError> {
    let explicit_profile = first_non_empty([
        global.profile.clone(),
        env::var("TOOL_PROFILE").ok(),
    ]);

    let explicit_host = first_non_empty([
        global.host.clone(),
        env::var("TOOL_HOST").ok(),
    ])
    .map(|raw| normalize_host_input(&raw))
    .transpose()?;

    let profile = if let Some(profile) = explicit_profile.clone() {
        profile
    } else if let Some(ref host) = explicit_host {
        select_profile_for_host(&canonical_host_key(host)?, config)?
            .or_else(|| config.active_profile.clone())
            .unwrap_or_else(|| "default".to_string())
    } else {
        config
            .active_profile
            .clone()
            .unwrap_or_else(|| "default".to_string())
    };

    let profile_cfg = config.profiles.get(&profile).cloned().unwrap_or_default();
    let profile_host = profile_cfg
        .host
        .as_deref()
        .map(normalize_host_input)
        .transpose()?;

    let host = match explicit_host {
        Some(host) => {
            if explicit_profile.is_some() {
                if let Some(ref profile_host) = profile_host {
                    if canonical_host_key(profile_host)? != canonical_host_key(&host)? {
                        return Err(CliError::usage(format!(
                            "profile '{profile}' is configured for '{profile_host}', but the target host is '{host}'"
                        )));
                    }
                }
            }
            host
        }
        None => profile_host.clone().unwrap_or_else(|| "https://api.example.test".to_string()),
    };

    let profile_matches_host = profile_host
        .as_deref()
        .map(canonical_host_key)
        .transpose()?
        .as_deref()
        == Some(&canonical_host_key(&host)?);

    let token_from_flag = first_non_empty([global.token.clone()]);
    let token_from_env = first_non_empty([env::var("TOOL_TOKEN").ok()]);
    let token = token_from_flag
        .clone()
        .or(token_from_env.clone())
        .or_else(|| profile_matches_host.then_some(profile_cfg.token.clone()).flatten());

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
        host,
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

fn select_profile_for_host(host_key: &str, config: &Config) -> Result<Option<String>, CliError> {
    if let Some(profile) = config.host_defaults.get(host_key) {
        return Ok(Some(profile.clone()));
    }

    let matching_profiles = config
        .profiles
        .iter()
        .filter_map(|(name, profile)| {
            let profile_key = canonical_host_key_opt(profile.host.as_deref())?;
            (profile_key == host_key).then_some(name.clone())
        })
        .collect::<Vec<_>>();

    match matching_profiles.len() {
        0 => Ok(None),
        1 => Ok(matching_profiles.into_iter().next()),
        _ => Err(CliError::usage(format!(
            "multiple profiles match '{host_key}'. Pass --profile or define a host default."
        ))),
    }
}

pub fn normalize_host_input(raw: &str) -> Result<String, CliError> {
    let trimmed = raw.trim();
    let normalized = if trimmed.contains("://") {
        trimmed.to_string()
    } else {
        format!("https://{trimmed}")
    };

    let url = Url::parse(&normalized)
        .map_err(|err| CliError::usage(format!("invalid host url: {err}")))?;

    let scheme = url.scheme().to_ascii_lowercase();
    let host = url
        .host_str()
        .ok_or_else(|| CliError::usage(format!("invalid host url: missing hostname in '{raw}'")))?;

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

pub fn canonical_host_key(raw: &str) -> Result<String, CliError> {
    let normalized = normalize_host_input(raw)?;
    let url = Url::parse(&normalized)
        .map_err(|err| CliError::usage(format!("invalid host url: {err}")))?;

    let scheme = url.scheme().to_ascii_lowercase();
    let host = url
        .host_str()
        .ok_or_else(|| CliError::usage(format!("invalid host url: missing hostname in '{raw}'")))?;

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

    Ok(format!("{scheme}://{}{port}", host.to_ascii_lowercase()))
}

fn canonical_host_key_opt(raw: Option<&str>) -> Option<String> {
    canonical_host_key(raw?).ok()
}

fn first_non_empty<const N: usize>(candidates: [Option<String>; N]) -> Option<String> {
    candidates
        .into_iter()
        .flatten()
        .find(|value| !value.trim().is_empty())
}
