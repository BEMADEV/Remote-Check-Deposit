# First Citizens Bank X9.37 Integration — Research and Implementation

**Project:** Summit Church / Simple — Remote Check Deposit via Rock RMS
**Bank:** First Citizens Bank (FCB), Raleigh NC
**Plugin:** BEMADEV/Remote-Check-Deposit (Rock RMS plugin)
**Date:** May 2026
**Author:** Jeff Ward
**Status:** Component built, validated end-to-end with FCB. PR pending on branch `feature/first-citizens-bank-component` of fork `jsw-austin/Remote-Check-Deposit`.

---

## 1. Context and Goal

Summit Church wants Rock RMS to be able to package scanned check images into the X9.37 "Image Cash Letter" file format that First Citizens Bank accepts for electronic deposit. Today they deposit using a manual or third-party process. Moving to a Rock-generated X9.37 file eliminates that step and matches what other Rock churches do with their banks.

The community-maintained BEMADEV Remote-Check-Deposit plugin already supports 17 different US banks (Regions, PNC, BB&T, Bank of America, Fifth Third, etc.) by providing a bank-specific C# class for each. There is no class for First Citizens Bank. We needed to:

1. Determine whether the plugin's **generic** X9.37 component (`X937V2DSTU`) could produce a file FCB will accept
2. If not, identify the specific gap(s)
3. Build a dedicated `FirstCitizensBank` component matching the pattern of the other bank classes

---

## 2. Background on the Format

### What X9.37 is

X9.37 is an ANSI standard for transmitting check image data between banks and originators. The base spec is **DSTU X9.37-2003**, later superseded by **ANS X9.100-180-2006**. A single X9.37 file is a sequence of fixed-position EBCDIC-encoded records, each preceded by a 4-byte big-endian length prefix.

Records in a typical file appear in this order:

```
Type 01  File Header        (one per file)
Type 10  Cash Letter Header (one per file)
Type 20  Bundle Header      (one or more per file)
  Type 25  Check Detail     (one per item, including the deposit credit)
  Type 50  Image View Detail          (one per image)
  Type 52  Image View Data            (one per image — contains TIFF bytes)
Type 70  Bundle Control     (one per bundle)
Type 90  Cash Letter Control (one per file)
Type 99  File Control       (one per file)
```

Each record type has a strict field layout. Field positions are 1-indexed in the spec.

### How BEMA's plugin is structured

The plugin defines a base class `X937V2DSTU` (in `FileFormatTypes/X937/X937V2DSTU.cs`) with all the configuration attributes Rock admins see in the UI (BOFD Routing Number, ECE Institution Routing Number, Origin Name, etc.) and methods that emit each record type. Bank-specific subclasses (e.g., `Regions`, `PNC`, `BBandT`) inherit from it and override methods where their bank's spec differs.

Configuration is done in Rock's admin UI by creating a File Format entity and selecting which component to use. The user had configured a `X937V2DSTU` (generic) instance for FCB.

---

## 3. Investigation Method

The work proceeded in five phases:

### Phase 1 — Read the FCB spec

I read `FCB X9 37 Specification.pdf` and the companion `FCB File Format Setup.xlsx` cheat sheet. The PDF lists each record type, every field's position, size, type, and any mandatory values. The Excel sheet condenses the field-specific FCB values (e.g., Destination Routing `053100300`, Immediate Destination Name `First Citizens NC`, deposit On-Us `nnnnnnnnnnnn/01`).

Both files originated from FCB's onboarding team.

### Phase 2 — Review the BEMADEV plugin source

I pulled the BEMADEV GitHub repo and read:

- `FileFormatTypes/X937/X937V2DSTU.cs` — the modern generic component
- `FileFormatTypes/X937/X937DSTU.cs` — the older base class
- `FileFormatTypes/X937/Regions.cs`, `BBandT.cs`, `PNC.cs` — examples of bank-specific subclasses
- `Records/X937/CashLetterHeader.cs`, `FileControl.cs`, `BundleControl.cs`, etc. — the record-layout definitions
- `Attributes/TextFieldAttribute.cs`, `MoneyFieldAttribute.cs`, `IntegerFieldAttribute.cs` — the encoding logic

### Phase 3 — Generate a test file with the generic component

The user configured a `X937V2DSTU` file format in their Rock instance with the values from the FCB Excel cheat sheet (Destination Routing `053100300`, ECE Institution Routing `053100300`, Origin Name `THE SUMMIT CHURCH...`, Credit Record Type = `Type25`, Payor Bank Routing `500901007`, On-Us Account `000863107938`, etc.) and clicked Export.

### Phase 4 — Parse the test file and compare to spec, byte by byte

I wrote a Python script that:

1. Reads the file
2. Parses each record using the 4-byte big-endian length prefix
3. Decodes records using EBCDIC (`cp037`)
4. For each record, extracts each FCB-specified field by position and width
5. Compares the extracted value against the spec's mandatory values, expected format, and FCB's notes

This is the same parsing FCB's validator does.

### Phase 5 — Send to FCB for acceptance validation

After working through the configuration issues, a clean file was submitted to FCB's onboarding team for validation against their actual parser. FCB confirmed that the file structure was acceptable in all respects **except one**: the ECE Institution Routing Number in the Type 10 Cash Letter Header record was missing its leading zero.

That single gap is the focus of this PR. Other apparent issues identified during the byte-level analysis (e.g., totals including the credit Type 25, Type 26 records being emitted, Type 99 Field 8 left blank) turned out to be acceptable to FCB in practice — the spec wording is more conservative than the actual parser requires.

---

## 4. Configuration Issues (Fixed Before Code Work)

Before the code-level investigation began, several attribute-value issues on the configured File Format were corrected. These are not changes to the plugin — they were changes the user made in the Rock admin UI.

| Issue | Before | After |
|---|---|---|
| Immediate Destination Name truncated | `First Citizens Ban` | `First Citizens NC` |
| ECE Institution Name truncated | `First Citizens Ban` | `First Citizens NC` |
| Originator Contact Phone has dashes; truncated | `919-354-59` | `9193545965` |
| Deposit On-Us was the routing number | `        053100300/01` | `     000863107938/01` |
| Payor Bank Routing Number was 8 digits, caused substring exception on export | `50090100` | `500901007` |

The substring exception traced to this code in `X937DSTU.cs`:

```csharp
PayorBankRoutingNumber = payorRoutingNumber.Substring( 0, 8 ),
PayorBankRoutingNumberCheckDigit = payorRoutingNumber.Substring( 8, 1 ),
```

An 8-character string has no character at position 8, so `Substring(8, 1)` throws `ArgumentOutOfRangeException`. Providing the full 9-digit routing number including the check digit (`500901007`) resolved it.

---

## 5. Code Issue Requiring a Subclass: Type 10 Routing Number Loses Leading Zero

### Evidence from the test file

| Record | Field | Bytes (EBCDIC decoded, positions 14–22) |
|---|---|---|
| Type 10 (Cash Letter Header) Field 4 | ECE Inst Routing | `53100300 ` ❌ |
| Type 20 (Bundle Header) Field 4 | ECE Inst Routing | `053100300` ✓ |

Same configuration value (`053100300`). Two different outputs in the same file.

### Root cause

The buggy code path is in `X937V2DSTU.GetCashLetterHeaderRecord()`:

```csharp
var institutionRoutingNumber = int.Parse(
    GetValueWithFallback( options, AttributeKey.InstitutionRoutingNumber, AttributeKey.ObsoleteAccountNumber ) );
// ...
ClientInstitutionRoutingNumber = institutionRoutingNumber.ToStringSafe(),
```

Step by step:

1. `int.Parse("053100300")` → integer `53100300` (leading zero is gone — that's how integers work)
2. `.ToStringSafe()` → `"53100300"` (8 characters, no padding)
3. Assignment to `ClientInstitutionRoutingNumber`, which is a `TextField(4, 9)` property on `CashLetterHeader`
4. `TextFieldAttribute.EncodeField` is left-justified by default → it right-pads the 8-character string with a space to reach 9 characters → `"53100300 "`

`GetBundleHeader()` for Type 20 reads the same attribute but keeps it as a string throughout, so leading zeros survive there:

```csharp
string institutionRoutingNumber = GetValueWithFallback( options, AttributeKey.InstitutionRoutingNumber, ... );
// (no int.Parse — stays as string)
header.ClientInstitutionRoutingNumber = institutionRoutingNumber;
```

### Why other banks don't hit this

Most existing bank subclasses (Regions, PNC, etc.) override `GetCashLetterHeaderRecord` themselves to do something custom with the institution routing number, and in doing so bypass the `int.Parse` path. The generic `X937V2DSTU` is the only component that hits the bug directly.

### Why no configuration setting can fix it

The bug is in the code path, not the value. Padding the input with extra leading zeros doesn't help — `int.Parse("0053100300")` still produces `53100300`. There is no attribute that bypasses the parse step.

---

## 6. Solution

A new component `FirstCitizensBank` that inherits from `X937V2DSTU` and overrides exactly one method: `GetCashLetterHeaderRecord`. The override calls the base method to get the standard header, then re-assigns `ClientInstitutionRoutingNumber` directly from the configured value as a string — bypassing the `int.Parse + ToStringSafe` path that loses the leading zero.

```csharp
protected override CashLetterHeader GetCashLetterHeaderRecord( ExportOptions options )
{
    var header = base.GetCashLetterHeaderRecord( options );

    string institutionRoutingNumber = GetValueWithFallback( options, "InstitutionRoutingNumber", "RoutingNumber" );
    if ( !string.IsNullOrWhiteSpace( institutionRoutingNumber ) )
    {
        header.ClientInstitutionRoutingNumber = institutionRoutingNumber;
    }

    return header;
}
```

That's the entire diff (aside from class declaration and one csproj line registering the file for compilation).

Summit's deployment path: after this PR is merged, Summit pulls the updated plugin and changes their File Format component from `X937V2DSTU` to `First Citizens Bank`. All existing configured attribute values carry over because the keys are identical (they're all defined on the `X937V2DSTU` base class).

---

## 7. References

- First Citizens Bank, *Commercial Image Cash Letter User Guide: X9.37 File Specification for Single and Multi Deposit Formats*, Version 1.0, October 12, 2023. Provided to Summit by FCB's onboarding team. Local copy: `/Users/jeffward/Downloads/FCB X9 37 Specification.pdf`
- First Citizens Bank, *FCB File Format Setup.xlsx* — companion cheat sheet of required field values. Local copy: `/Users/jeffward/Downloads/FCB File Format Setup.xlsx`
- ANSI X9.37 (DSTU X9.37-2003), *Specifications for Electronic Exchange of Check and Image Data*
- ANS X9.100-180-2006, *Specifications for Electronic Exchange of Check and Image Data* (replacement for DSTU X9.37-2003)
- BEMADEV/Remote-Check-Deposit: https://github.com/BEMADEV/Remote-Check-Deposit
- Working fork: https://github.com/jsw-austin/Remote-Check-Deposit
- Working branch: `feature/first-citizens-bank-component`
