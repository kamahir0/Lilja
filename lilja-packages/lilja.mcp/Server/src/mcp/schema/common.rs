use serde::{Deserialize, Serialize};

#[derive(Serialize, Deserialize, Debug, Clone, PartialEq, Eq, Hash)]
pub struct ToolName(String);

impl ToolName {
    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl From<String> for ToolName {
    fn from(s: String) -> Self {
        Self(s)
    }
}

impl From<&str> for ToolName {
    fn from(s: &str) -> Self {
        Self(s.to_string())
    }
}
