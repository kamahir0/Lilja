
# Instruction for the AI Agent

**Objective:**
Based on the `Runtime/Core` files you just implemented, now implement the **Source Generator** component.
This is **Phase 2** of the implementation.

**Context:**
The Runtime Core (Attributes, Interfaces) is already established. Now we need the "Engine" that scans these attributes and generates the boiler-plate code.

**Requirements:**
Please strictly follow the "High-Fidelity Technical Specification" below to implement the Source Generator.

---

# High-Fidelity Technical Specification: Lilja.Repository.Generator

## 1. Project Context

**Target Module:** `Lilja.Repository.Generator`
**Type:** Roslyn Source Generator (IIncrementalGenerator)
**Purpose:**
Scan user code for `[Entity]` and `[ToPrimitive]` attributes, and generate:

1. **DTOs:** Flattened `[Serializable]` classes.
2. **Transfers:** Explicit implementation of `ITransferable<TDto>` on Entities (Backdoor).
3. **Formatters:** Dependency-free MessagePack formatters.
4. **Repositories:** Interface and Basic InMemory implementation.

## 2. Technical Stack

* **Language:** C# 10.0
* **Target Framework:** .NET Standard 2.0 (Strict requirement for Unity Roslyn Analyzers).
* **Dependencies:**
* `Microsoft.CodeAnalysis.CSharp`
* `Microsoft.CodeAnalysis.CSharp.Workspaces`



## 3. Directory Structure

The Generator must be isolated from the Runtime code.

```text
src/Scripts/
├── Editor/
│   └── Generator/
│       ├── Lilja.Repository.Generator.csproj  <-- IMPORTANT: Needs independent project file
│       ├── LiljaRepositoryGenerator.cs        <-- Main Entry
│       ├── Receivers/                         (Syntax Receivers/Providers)
│       └── Emitters/                          (String Builders)

```

## 4. Generation Logic & Rules

### 4.1. Analysis Phase (The Receiver)

* **Target:** Classes annotated with `Lilja.Repository.Core.Attributes.EntityAttribute`.
* **Field Scanning:**
* Look for fields annotated with `[Persist(index)]`.
* **ValueObject Detection:** If a field's type has a method annotated with `[ToPrimitive]`, treat it as a ValueObject.
* **Tuple Parsing:** Parse the return type of `[ToPrimitive]` (e.g., `(int x, int y)`) to determine the flattened fields.



### 4.2. Emission Phase 1: DTOs

* **Namespace:** `Lilja.Generated.Dtos`
* **Class:** `[System.Serializable] public class {EntityName}Dto`
* **Fields:**
* Public fields matching the types found in Analysis.
* For ValueObjects, generate flattened fields (e.g., `Location_x`, `Location_y`).



### 4.3. Emission Phase 2: Entity Implementation

* **Target:** `partial class {EntityName}`
* **Interface:** `Lilja.Repository.Core.Interfaces.ITransferable<Lilja.Generated.Dtos.{EntityName}Dto>`
* **Logic:**
* `Export()`: Map private fields (`_hp`) and ValueObjects (`_loc.AsPrimitive()`) to DTO fields.
* `Import()`: Map DTO fields back to private fields and reconstruct ValueObjects (`new Coordinate(dto.x, dto.y)`).



### 4.4. Emission Phase 3: Formatters (Dependency-Free)

* **Namespace:** `Lilja.Generated.Formatters`
* **Class:** `public sealed class {EntityName}DtoFormatter : IMessagePackFormatter<{EntityName}Dto>`
* **Logic:**
* `Serialize`: Use `writer.WriteArrayHeader(n)` and `writer.WriteInt32`, `writer.WriteString`, etc.
* `Deserialize`: Use `reader.ReadArrayHeader()` and `reader.ReadInt32`, etc.
* **Crucial:** Do NOT rely on `MessagePack.SourceGenerator`. Generate raw `Write/Read` calls based on the primitive types identified in the Analysis phase.



### 4.5. Emission Phase 4: Repositories

* **Namespace:** User's namespace + `.Repositories`
* **Interface:** `I{EntityName}Repository`
* Methods: `Read`, `Update` (plus `Create`, `Delete` if `[Key]` exists).
* Use `IReadableTx` and `IReadWriteTx` from Core.


* **InMemory:** `InMemory{EntityName}Repository` implementing the interface using a `Dictionary` (if Keyed) or `Field` (if Singleton).

---

## 5. Constraints

* **Performance:** Use `StringBuilder` for text generation. Avoid LINQ in the hot path of the generator.
* **Error Handling:** If `[ToPrimitive]` returns a non-primitive or non-tuple, emit a Diagnostic Error (Compile Error).
* **Code Style:** Generated code should disable nullability warnings (`#nullable disable`).

---

## 6. Definition of Done

1. **Project File:** `.csproj` is created correctly for a Roslyn Analyzer.
2. **Generator Code:** `IIncrementalGenerator` implementation is complete.
3. **Tests:** A snapshot testing setup (or a clear explanation of how to test) is provided.

---

**Generate the Source Generator implementation code based on these specifications.**