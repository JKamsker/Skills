use std::io::{self, IsTerminal, Write};

use serde::Serialize;
use serde_json::Value;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum OutputFormat {
    Table,
    Json,
    Raw,
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
    Usage = 2,
    NotAuthenticated = 3,
    Network = 8,
    Cancelled = 10,
}

#[derive(Debug, Clone)]
pub struct CliError {
    pub exit: ExitCategory,
    pub message: String,
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
    preview: impl FnOnce() -> String,
) -> Result<GuardDecision, CliError> {
    if mode.dry_run {
        println!("{}", preview());
        return Ok(GuardDecision::DryRunPrinted);
    }

    if mode.yes {
        return Ok(GuardDecision::Continue);
    }

    if mode.quiet || !io::stdin().is_terminal() || !io::stderr().is_terminal() {
        return Err(CliError {
            exit: ExitCategory::Cancelled,
            message: "confirmation required. Re-run with --yes or --dry-run.".to_string(),
        });
    }

    eprint!("{prompt} [y/N]: ");
    io::stderr().flush().map_err(io_error)?;

    let mut input = String::new();
    io::stdin().read_line(&mut input).map_err(io_error)?;
    let answer = input.trim().to_ascii_lowercase();

    Ok(if matches!(answer.as_str(), "y" | "yes") {
        GuardDecision::Continue
    } else {
        GuardDecision::Cancelled
    })
}

pub fn write_value<T: Serialize>(value: &T, format: OutputFormat) -> Result<(), CliError> {
    match format {
        OutputFormat::Json => {
            println!("{}", serde_json::to_string_pretty(value).map_err(json_error)?);
        }
        OutputFormat::Raw => {
            println!("{}", serde_json::to_string(value).map_err(json_error)?);
        }
        OutputFormat::Table => {
            let value = serde_json::to_value(value).map_err(json_error)?;
            write_tableish_value(&value)?;
        }
    }

    Ok(())
}

pub fn write_raw_bytes(bytes: &[u8]) -> Result<(), CliError> {
    let mut stdout = io::stdout().lock();
    stdout.write_all(bytes).map_err(io_error)?;
    Ok(())
}

pub fn print_dry_run(method: &str, url: &str, headers: &[(&str, &str)]) -> Result<(), CliError> {
    println!("{method} {url}");
    for (name, value) in headers {
        let display = if name.eq_ignore_ascii_case("authorization") || name.eq_ignore_ascii_case("cookie") {
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
        exit: ExitCategory::Network,
        message: err.to_string(),
    }
}

fn json_error(err: serde_json::Error) -> CliError {
    CliError {
        exit: ExitCategory::Usage,
        message: err.to_string(),
    }
}
