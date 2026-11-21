# Catalyst: TypeScript Schema Rules
This section details the TypeScript-specific attributes and type conversions used during code generation.

## Compiler Options
### File Options
Currently no file-specific options. Use the `--TsSchema` compiler flag to select the definition representation for the whole run.

### Enum Options
Currently no enum-specific options.

### Definition Options
Currently no definition-specific options.

### Property Options
Currently no property-specific options.

### Service Options
Currently no service-specific options.

### Endpoint Options
Currently no endpoint-specific options.

## Validator System
The TypeScript generator does not currently include a Validator system that generates validation code.

## Property Type Conversions
| Schema Type                  | TypeScript Type | Notes                                                           |
|------------------------------|-----------------|-----------------------------------------------------------------|
| `str`                        | `string`        |                                                                 |
| `i32`                        | `number`        |                                                                 |
| `i64`                        | `bigint`        |                                                                 |
| `f64`                        | `number`        |                                                                 |
| `bool`                       | `boolean`       |                                                                 |
| `date`                       | `string`        | ISO 8601 string                                                 |
| `time`                       | `number`        | Seconds                                                         |
| `uuid`                       | `string`        |                                                                 |
| `url`                        | `string`        |
| `list<T>`                    | `Array<T>`      | Where `T` is the TS equivalent of the inner schema type.        |
| `set<T>`                     | `Array<T>`      | Emitted as arrays in TS.                                        |
| `map<K, V>`                  | `Record<K, V>`  | Where `K` and `V` are the TS equivalents of the schema types.   |
| `any`                        | `any`           |                                                                 |
| `[Namespace.]DefinitionName` | `ClassName`     | Reference to a generated TypeScript class (no namespaces).      |

Notes:
- Optional schema types are emitted as `T | null`, and properties use `?` in classes.
- Generated classes include `toBytes()`/`fromBytes(bytes)` helpers using JSON and TextEncoder/TextDecoder.
- Client services generate `XxxClient` with `fetch`-based calls.

## Schema Mode
Definitions are generated as plain TypeScript classes by default. Use Effect Schema instead by setting `--TsSchema=effect`:
```sh
dotnet run --project . --Language=ts --TsSchema=effect --BaseInputDir=./TestData --BaseOutputDir=./output/ts-effect
```

`--TsSchema` accepts `class` (the default) or `effect`. It is a compiler flag rather than a file-level `ts:` option because generated files import each other; switching only part of a spec would mix class modules and schema modules. `--TsSchema=class` keeps the existing output exactly.

## Effect Schema Mode
Each definition becomes a schema and the type users work with:
```ts
/**
 * Represents a user in the system.
 */
export const User = Schema.Struct({
    userId: Schema.Int,
    userUuid: Schema.optional(Schema.NullOr(Schema.UUID)),
    username: Schema.String,
    status: Schema.optional(Schema.NullOr(Schema.String))
});

export type User = typeof User.Type;
```

Property keys are the names written in the spec (the wire names). The class output uses PascalCase property names instead.

Recursive definitions, directly or through another definition, are emitted as an interface plus a typed schema. TypeScript cannot infer the type of a self-referential schema:
```ts
export interface Category {
    readonly name: string;
    readonly parentCategory?: Category | null;
}

export const Category: Schema.Schema<Category> = Schema.Struct({
    name: Schema.String,
    parentCategory: Schema.optional(Schema.NullOr(Schema.suspend(() => Category)))
});
```

### Type Conversions
| Schema Type                  | Effect Schema                        | TypeScript Type     | Notes                                                           |
|------------------------------|--------------------------------------|---------------------|-----------------------------------------------------------------|
| `str`                        | `Schema.String`                      | `string`            |                                                                 |
| `i32`                        | `Schema.Int`                         | `number`            |                                                                 |
| `i64`                        | `Schema.BigIntFromNumber`            | `bigint`            | JSON number to `bigint`; limited to JavaScript's safe integers.  |
| `f64`                        | `Schema.Number`                      | `number`            |                                                                 |
| `bool`                       | `Schema.Boolean`                     | `boolean`           |                                                                 |
| `date`                       | `Schema.String`                      | `string`            | ISO 8601 string on the wire.                                    |
| `time`                       | `Schema.Number`                      | `number`            | Seconds.                                                        |
| `uuid`                       | `Schema.UUID`                        | `string`            |                                                                 |
| `url`                        | `Schema.String`                      | `string`            |                                                                 |
| `list<T>`                    | `Schema.Array(...)`                  | `ReadonlyArray<T>`  | Where `T` is the Effect Schema for the inner type.              |
| `set<T>`                     | `Schema.Array(...)`                  | `ReadonlyArray<T>`  | Emitted as arrays, like the class output.                       |
| `map<K, V>`                  | `Schema.Record(...)`                 | `Record<string, V>` | JSON object keys are strings, so `K` is not reflected.          |
| `any`                        | `Schema.Any`                         | `any`               |                                                                 |
| `T?`                         | `Schema.optional(Schema.NullOr(T))`  | `T \| null`         | The property is optional on the wire.                           |
| `[Namespace.]DefinitionName` | `Schema.suspend(() => TypeName)`     | `TypeName`          | The laziness allows forward and recursive references.           |
| enum                         | `Schema.Enums(EnumName)`             | `EnumName`          | Flag enums use a generated `EnumNameSchema`; see below.         |

### Defaults, Constants and Enums
- An explicit default becomes `Schema.optionalWith(T, { default: () => value, exact: true })`. The property may be absent on the wire and is always present after decoding.
- A default on an optional `T?` property is ignored, matching the class output.
- Constants are emitted in a companion object because a const schema cannot merge with a namespace:
  ```ts
  export const ConstantTestConstants = {
      IntConstant: 42
  } as const;
  ```
- A normal enum uses `Schema.Enums(ProductType)` over the generated TypeScript enum. A flag enum, such as `UserRole`, uses a generated `UserRoleSchema` const that accepts any combination of the declared bits and `-1`, because `Schema.Enums` rejects combinations such as `Moderator | Admin`.

### Client Methods
The fetch client keeps its class shape and `Promise<T>` return type. In effect mode it encodes the request with `Schema.encodeSync` and decodes the response with `Schema.decodeUnknownSync`, so a malformed response throws a `ParseError` at the boundary:
```ts
public async GetUser(request: GetUserRequest): Promise<GetUserResponse> {
    const url = this.options.baseUrl + "/Users" + "/get";
    const body = JSON.stringify(Schema.encodeSync(GetUserRequest)(request));
    const bodyBytes = new TextEncoder().encode(body);
    const res = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json; charset=utf-8' }, body: bodyBytes } as RequestInit);
    if (!res.ok) throw new Error('HTTP ' + res.status);
    const bytes = new Uint8Array(await res.arrayBuffer());
    const json = new TextDecoder().decode(bytes);
    return Schema.decodeUnknownSync(GetUserResponse)(JSON.parse(json));
}
```

### Limitations
- `date` stays a `string` and `url` stays a `string`. `Schema.Date` and `Schema.URL` would decode to `Date` and `URL`, which does not match the class output.
- `map<K, V>` uses `Record<string, V>`. A numeric or enum key type is not reflected in the decoded type.
- `min`, `max` and `pattern` are still emitted as JSDoc metadata, not as schema refinements.
- Only an explicit `default:` becomes a schema default. The class output also initialises strings, lists, maps and `any` to empty values without a spec default; Effect Schema mode keeps those properties required.
- `any` is treated as optional, matching the class output.
