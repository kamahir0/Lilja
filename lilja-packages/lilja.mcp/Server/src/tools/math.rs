use super::Tool;
use schemars::JsonSchema;
use serde::Deserialize;
use serde_json::Value;

#[derive(Deserialize, JsonSchema)]
pub struct MathArgs {
    /// First number
    pub a: f64,
    /// Second number
    pub b: f64,
}

pub struct AddTool;
impl Tool for AddTool {
    type Args = MathArgs;
    fn name(&self) -> &'static str {
        "add"
    }
    fn description(&self) -> &'static str {
        "Adds two numbers"
    }
    fn handle(&self, args: Self::Args) -> Result<Value, String> {
        Ok(serde_json::json!(args.a + args.b))
    }
}

pub struct SubtractTool;
impl Tool for SubtractTool {
    type Args = MathArgs;
    fn name(&self) -> &'static str {
        "subtract"
    }
    fn description(&self) -> &'static str {
        "Subtracts second number from first number"
    }
    fn handle(&self, args: Self::Args) -> Result<Value, String> {
        Ok(serde_json::json!(args.a - args.b))
    }
}

pub struct MultiplyTool;
impl Tool for MultiplyTool {
    type Args = MathArgs;
    fn name(&self) -> &'static str {
        "multiply"
    }
    fn description(&self) -> &'static str {
        "Multiplies two numbers"
    }
    fn handle(&self, args: Self::Args) -> Result<Value, String> {
        Ok(serde_json::json!(args.a * args.b))
    }
}

pub struct DivideTool;
impl Tool for DivideTool {
    type Args = MathArgs;
    fn name(&self) -> &'static str {
        "divide"
    }
    fn description(&self) -> &'static str {
        "Divides first number by second number"
    }
    fn handle(&self, args: Self::Args) -> Result<Value, String> {
        if args.b == 0.0 {
            Err("Division by zero".to_string())
        } else {
            Ok(serde_json::json!(args.a / args.b))
        }
    }
}
