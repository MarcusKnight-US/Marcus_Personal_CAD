# AppConfig.md

## Purpose
`AppConfig.cs` defines a **singleton** configuration class that holds default settings for the converter (DPI, background colour, paper size, etc.) and can load overrides from a `config.json` file.

## Key C# concepts demonstrated
- **Properties with default values** – e.g., `public int DefaultDpi { get; set; } = 200;`
- **Nullable reference type** – `private static AppConfig? _instance;`
- **Singleton pattern** – static `Instance` property lazily creates the object via `Load()`.
- **File I/O** – `Path.Combine`, `File.Exists`, `File.ReadAllText`.
- **JSON deserialization** – `JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })`.
- **String interpolation** – `$"Loaded config from: {foundPath}"`.
- **Exception handling** – `try/catch` around file reading.

## Simplified flow (pseudocode)
```
if (AppConfig.Instance not created)
    try to locate a config.json in several possible locations
    if found
        read JSON → deserialize into AppConfig object
    else
        use default property values
return the singleton instance
```

## Notable members
- `DefaultDpi`, `BackgroundColor`, `MinLayoutWidth` … – configurable values.
- `Instance` – global access point.
- `Reload()` – force re‑load from file.

*All other `.cs` files follow a similar documentation pattern.*
