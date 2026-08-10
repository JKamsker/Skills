use std::io::{self, IsTerminal, Write};

use serde::Serialize;
use serde_json::Value;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum OutputFormat {
    Table,
    /// Machine output (JSON, pretty-printed).
    Json,
    /// Machine output (JSON, compact). Raw bytes remain a separate non-machine mode; see `write_raw_bytes`.
    JsonCompact,
    /// Raw bytes on stdout. This mode is non-human and must not prompt.
    RawBytes,
}

#[derive(Debug, Clone, Copy)]
pub struct RunMode {
    pub output: OutputFormat,
    pub dry_run: bool,
    pub yes: bool,
    pub quiet: bool,
    pub verbose: u8,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum GuardDecision {
    Continue,
    DryRunPrinted,
    Cancelled,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ExitCategory {
    Success = 0,
    Runtime = 1,
    Usage = 2,
    NotAuthenticated = 3,
    Network = 8,
    Cancelled = 10,
}

#[derive(Debug, Clone)]
pub struct CliError {
    pub exit: ExitCategory,
    pub message: String,
    pub already_rendered: bool,
}

const JSON_SCHEMA_VERSION: u32 = 1;

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct JsonMeta {
    pub schema_version: u32,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct JsonError {
    pub kind: String,
    pub message: String,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct JsonEnvelope<'a, T: Serialize> {
    pub ok: bool,
    pub data: Option<&'a T>,
    pub error: Option<JsonError>,
    pub meta: JsonMeta,
}

pub fn should_print_header(force: bool, suppress: bool) -> bool {
    if suppress {
        return false;
    }
    if force {
        return true;
    }
    io::stdout().is_terminal()
}

pub fn confirm_or_abort(
    mode: RunMode,
    prompt: &str,
    preview: impl FnOnce(OutputFormat) -> Result<(), CliError>,
) -> Result<GuardDecision, CliError> {
    if mode.dry_run {
        preview(mode.output)?;
        return Ok(GuardDecision::DryRunPrinted);
    }

    if mode.yes {
        return Ok(GuardDecision::Continue);
    }

    if matches!(mode.output, OutputFormat::Json | OutputFormat::JsonCompact) {
        let mut error = CliError {
            exit: ExitCategory::Usage,
            message: "Confirmation required. Use --yes to confirm or --dry-run to preview. Prompts are disabled in machine output modes."
                .to_string(),
            already_rendered: false,
        };
        write_error(&mut error, mode.output)?;
        return Err(error);
    }

    if matches!(mode.output, OutputFormat::RawBytes) {
        return Err(CliError {
            exit: ExitCategory::Usage,
            message: "Confirmation required. Use --yes to confirm or --dry-run to preview. Prompts are disabled when stdout is reserved for raw bytes.".to_string(),
            already_rendered: false,
        });
    }

    if mode.quiet || !io::stdin().is_terminal() || !io::stderr().is_terminal() {
        return Err(CliError {
            // Interaction-required refusal (quiet / non-TTY) is exit 2, not exit 10.
            exit: ExitCategory::Usage,
            message: "Confirmation required. Use --yes to confirm or --dry-run to preview.".to_string(),
            already_rendered: false,
        });
    }

    eprint!("{prompt} Type 'yes' to confirm: ");
    io::stderr().flush().map_err(io_error)?;

    let mut input = String::new();
    io::stdin().read_line(&mut input).map_err(io_error)?;
    let answer = input.trim().to_ascii_lowercase();

    Ok(if answer == "yes" {
        GuardDecision::Continue
    } else {
        // Caller should map this explicit cancellation to exit 10.
        GuardDecision::Cancelled
    })
}

pub fn write_value<T: Serialize>(value: &T, format: OutputFormat) -> Result<(), CliError> {
    match format {
        OutputFormat::Json => {
            write_json_envelope(value, None::<JsonError>, format)?;
        }
        OutputFormat::JsonCompact => {
            write_json_envelope(value, None::<JsonError>, format)?;
        }
        OutputFormat::Table => {
            let value = serde_json::to_value(value).map_err(json_error)?;
            write_tableish_value(&value)?;
        }
        OutputFormat::RawBytes => {
            return Err(CliError {
                exit: ExitCategory::Usage,
                message: "structured values cannot be emitted while raw-byte stdout is selected".to_string(),
                already_rendered: false,
            });
        }
    }

    Ok(())
}

pub fn write_error(error: &mut CliError, format: OutputFormat) -> Result<(), CliError> {
    if error.already_rendered {
        return Ok(());
    }

    match format {
        OutputFormat::Json | OutputFormat::JsonCompact => {
            let json_error = JsonError {
                kind: kind_for_exit(error.exit).to_string(),
                message: error.message.clone(),
            };
            write_json_envelope(Value::Null, Some(json_error), format)?;
        }
        OutputFormat::Table | OutputFormat::RawBytes => {
            eprintln!("Error: {}", error.message);
        }
    }

    error.already_rendered = true;
    Ok(())
}

pub fn write_raw_bytes(bytes: &[u8], format: OutputFormat) -> Result<(), CliError> {
    if !matches!(format, OutputFormat::RawBytes) {
        return Err(CliError {
            exit: ExitCategory::Usage,
            message: "raw-byte stdout requires selecting the raw-bytes output mode".to_string(),
            already_rendered: false,
        });
    }

    let mut stdout = io::stdout().lock();
    stdout.write_all(bytes).map_err(io_error)?;
    Ok(())
}

/// Human-readable dry-run preview (table mode). In machine output modes, emit a structured preview via `write_value`.
pub fn print_dry_run_table(method: &str, url: &str, headers: &[(&str, &str)]) -> Result<(), CliError> {
    println!("{method} {url}");
    for (name, value) in headers {
        let display = if name.eq_ignore_ascii_case("authorization")
            || name.eq_ignore_ascii_case("cookie")
            || name.eq_ignore_ascii_case("set-cookie")
            || name.eq_ignore_ascii_case("x-api-key")
            || name.eq_ignore_ascii_case("x-auth-token")
            || name.eq_ignore_ascii_case("x-access-token")
        {
            "REDACTED"
        } else {
            value
        };
        println!("{name}: {display}");
    }
    Ok(())
}

fn write_tableish_value(value: &Value) -> Result<(), CliError> {
    match value {
        Value::Object(map) => {
            for (key, value) in map {
                println!("{key}: {}", scalar(value));
            }
        }
        Value::Array(rows) => {
            for row in rows {
                println!("{}", serde_json::to_string(row).map_err(json_error)?);
            }
        }
        _ => println!("{}", scalar(value)),
    }

    Ok(())
}

fn scalar(value: &Value) -> String {
    match value {
        Value::Null => String::new(),
        Value::Bool(value) => value.to_string(),
        Value::Number(value) => value.to_string(),
        Value::String(value) => value.clone(),
        _ => serde_json::to_string(value).unwrap_or_default(),
    }
}

fn io_error(err: io::Error) -> CliError {
    CliError {
        exit: ExitCategory::Runtime,
        message: err.to_string(),
        already_rendered: false,
    }
}

fn json_error(err: serde_json::Error) -> CliError {
    CliError {
        exit: ExitCategory::Runtime,
        message: format!("failed to serialize output: {err}"),
        already_rendered: false,
    }
}

fn write_json_envelope<T: Serialize>(
    value: T,
    error: Option<JsonError>,
    format: OutputFormat,
) -> Result<(), CliError> {
    let envelope = JsonEnvelope {
        ok: error.is_none(),
        data: error.is_none().then_some(&value),
        error,
        meta: JsonMeta {
            schema_version: JSON_SCHEMA_VERSION,
        },
    };

    match format {
        OutputFormat::Json => println!("{}", serde_json::to_string_pretty(&envelope).map_err(json_error)?),
        OutputFormat::JsonCompact => println!("{}", serde_json::to_string(&envelope).map_err(json_error)?),
        OutputFormat::Table | OutputFormat::RawBytes => unreachable!("non-JSON output does not use JSON envelopes"),
    }

    Ok(())
}

fn kind_for_exit(exit: ExitCategory) -> &'static str {
    match exit {
        ExitCategory::Success => "success",
        ExitCategory::Runtime => "runtime",
        ExitCategory::Usage => "refused",
        ExitCategory::NotAuthenticated => "not_authenticated",
        ExitCategory::Network => "network",
        ExitCategory::Cancelled => "cancelled",
    }
}
