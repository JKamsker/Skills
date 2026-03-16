use std::collections::BTreeMap;
use std::env;
use std::time::Duration;

use url::Url;

// This example implements the **hostname-key** target identity mode (the Jellyfin worked-example choice).
// - Network operations use a normalized base URL (scheme/port/path may matter).
// - Credentials and defaults bind to the hostname identity key (lowercased hostname).

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
            Some(target_key) => select_profile_for_target(target_key, config)?
                .or_else(|| config.active_profile.clone())
                .unwrap_or_else(|| "default".to_string()),
            None => config
                .active_profile
                .clone()
                .unwrap_or_else(|| "default".to_string()),
        },
    };

    let profile_cfg = config.profiles.get(&profile).cloned().unwrap_or_default();

    let profile_hostname = profile_cfg
        .hostname
        .as_deref()
        .map(normalize_hostname)
        .transpose()?;

    if explicit_profile.is_some() {
        if let (Some(ref explicit_target_key), Some(ref profile_hostname)) = (&explicit_target_key, &profile_hostname) {
            if explicit_target_key != profile_hostname {
                return Err(CliError::usage(format!(
                    "profile '{profile}' is configured for '{profile_hostname}', but the target is '{explicit_target_key}'"
                )));
            }
        }
    }

    let base_url = match explicit_base_url {
        Some(url) => url,
        None => match profile_cfg.base_url.as_deref() {
            Some(url) => normalize_base_url_input(url)?,
            None => match profile_hostname.as_deref() {
                Some(hostname) => normalize_base_url_input(hostname)?,
                None => "https://api.example.test".to_string(),
            },
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
        return Ok(Some(profile.clone()));
    }

    let matching_profiles = config
        .profiles
        .iter()
        .filter_map(|(name, profile)| {
            let hostname = profile.hostname.as_deref()?.trim();
            if hostname.is_empty() {
                return None;
            }

            let normalized = normalize_hostname(hostname).ok()?;
            (normalized == target_key).then_some(name.clone())
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
    } else {
        format!("https://{trimmed}")
    };

    let url = Url::parse(&normalized).map_err(|err| CliError::usage(format!("invalid target url: {err}")))?;

    let scheme = url.scheme().to_ascii_lowercase();
    let host = url
        .host_str()
        .ok_or_else(|| CliError::usage(format!("invalid target url: missing hostname in '{raw}'")))?;

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

    Ok(trimmed.to_ascii_lowercase())
}

pub fn target_identity_hostname_key(normalized_base_url: &str) -> Result<String, CliError> {
    let url = Url::parse(normalized_base_url).map_err(|err| CliError::usage(format!("invalid base url: {err}")))?;
    let host = url
        .host_str()
        .ok_or_else(|| CliError::usage(format!("invalid base url: missing hostname in '{normalized_base_url}'")))?;
    Ok(host.to_ascii_lowercase())
}

fn first_non_empty<const N: usize>(candidates: [Option<String>; N]) -> Option<String> {
    candidates
        .into_iter()
        .flatten()
        .find(|value| !value.trim().is_empty())
}
