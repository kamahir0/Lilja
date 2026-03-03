pub mod schema;

pub mod method {
    pub use super::schema::initialize::METHOD as INITIALIZE;
    pub use super::schema::tools_call::METHOD as TOOLS_CALL;
    pub use super::schema::tools_list::METHOD as TOOLS_LIST;
}
