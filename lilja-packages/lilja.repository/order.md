
# High-Fidelity Technical Specification for Lilja.Repository Implementation

## 1. Project Context

**Project Name:** Lilja.Repository
**Target Scope:** Package Core & Sample Implementation
**Purpose:**
This specification defines the implementation rules for the `Lilja.Repository` Unity package. The package provides a high-performance, transactional repository pattern using Source Generators.

**Crucial Directory Policy:**
Since this is a Unity Package, all core definitions (Attributes, Factories, Interfaces) that are shared across the ecosystem must be placed directly under the package root hierarchy, specifically within `Runtime/Core`.

---

## 2. Technical Stack & Dependencies

* **Engine:** Unity 6000.0.0f1 or later.
* **Language:** C# 10.0 (NET Standard 2.1).
* **Dependencies:**
* `Cysharp.Threading.Tasks` (UniTask)
* `VContainer` (optional, but assumed for DI patterns)



---

## 3. Architecture & Design Patterns

### 3.1. Package Core (Runtime/Core)

The following components **MUST** be implemented in `Runtime/Core` as they are foundational:

* **Attributes:** `[Entity]`, `[Key]`, `[Persist]`, `[ToPrimitive]`.
* **Interfaces:** `IReadableTx`, `IReadWriteTx`, `ITxFactory`.
* **Concrete Classes:** `TxFactory` (Default implementation of Transaction management).

### 3.2. Domain Layer Rules (User Code)

* **Entities:**
* Must be `partial class` annotated with `[Entity]`.
* Fields to be persisted must be `private` or `private set` with `[Persist(index)]`.


* **ValueObjects (Simplified):**
* **No Class Attribute:** Do NOT use a class-level `[ValueObject]` attribute.
* **Detection Rule:** Any struct/class containing a method annotated with **`[ToPrimitive]`** is treated as a ValueObject.
* **Flattening:** The `[ToPrimitive]` method must return a primitive or a `ValueTuple` (e.g., `(int x, int y)`) which the Source Generator will use to flatten the data into the DTO.



### 3.3. Source Generator Contract

* The Source Generator will scan for `[Entity]` and `[ToPrimitive]` to generate:
1. **DTOs:** Single `[Serializable]` class with flattened public fields.
2. **Backdoor:** `ITransferable<TDto>` implementation on the Entity.
3. **Formatters:** Custom MessagePack formatters (dependency-free).
4. **Repositories:** `I{Entity}Repository` interfaces.



---

## 4. Naming Conventions & Coding Standards

* **Namespace Root:** `Lilja.Repository` (and `Lilja.Core` for shared types).
* **Directory Structure:**
The file placement must strictly follow this layout relative to `package.json`:
```text
/ (Package Root)
├── package.json
├── Runtime/
│   ├── Core/               <-- PLACE CORE DEFINITIONS HERE
│   │   ├── Attributes/     ([Entity], [ToPrimitive], etc.)
│   │   ├── Transactions/   (ITxFactory, TxFactory, IReadableTx)
│   │   └── Interfaces/     (ITransferable, etc.)
│   ├── Infrastructure/     (Base Repository Implementations)
│   └── Generated/          (Target folder for SG output)
└── Tests/
    └── Editor/             (Unit Tests)

```



---

## 5. Detailed Functional Requirements

### Feature 1: Core Attributes Implementation

* **Location:** `Runtime/Core/Attributes`
* **Requirement:** Implement `EntityAttribute`, `KeyAttribute`, `PersistAttribute`, and `ToPrimitiveAttribute`.
* `ToPrimitiveAttribute` targets **Methods only**.



### Feature 2: Transaction Factory Implementation

* **Location:** `Runtime/Core/Transactions`
* **Requirement:** Implement `TxFactory` and interfaces.
* `TxFactory` must manage the lifecycle of transactions and provide `BeginRead()` and `BeginWrite()`.
* It must be a concrete class usable immediately by package users.



### Feature 3: Sample Implementation (for Validation)

Implement a sample "Item" Entity to verify the design.

* **ValueObject `Coordinate`:**
* Define `struct Coordinate { int X; int Y; }`.
* Implement `[ToPrimitive] public (int x, int y) Serialize() => (X, Y);`.


* **Entity `Item`:**
* Use `[Entity]` and `private` fields.
* Use `Coordinate` as a field to verify flattening logic.



---

## 6. Constraints & Edge Cases

* **No "Lilja" in Code:** Except for the namespace (`Lilja.*`), do not use the word "Lilja" in class names or method names (e.g., use `EntityAttribute`, NOT `LiljaEntityAttribute`).
* **Dependency Isolation:** Core attributes and interfaces must NOT depend on any external serialization library (MsgPack/JSON).

---

## 7. Definition of Done

1. **Structure:** `TxFactory` and Attributes exist under `Runtime/Core`.
2. **VO Detection:** The system correctly identifies `Coordinate` as a ValueObject solely via the `[ToPrimitive]` attribute.
3. **Compilation:** Validates with zero warnings in Unity.

---

**Generate the implementation code based on the specifications above.**