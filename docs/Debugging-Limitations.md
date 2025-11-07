# XSLT Debugger - Known Limitations and Behaviors

## Overview
This document describes the known limitations and expected behaviors of the XSLT Debugger, particularly around elements that cannot be debugged.

> **Quick Summary**: You cannot debug inside `xsl:attribute` elements or on closing tags. These are skipped by design to ensure correct XSLT output.

---

## 1. xsl:attribute Elements

### Cannot Set Breakpoints Inside Attributes

**Limitation:** You cannot set breakpoints or step into code inside `xsl:attribute` elements.

**Reason:** XSLT specification constraints - `xsl:attribute` can only contain text-generating instructions. Adding debug instrumentation would violate the spec and cause transformations to fail.

### Example

```xml
<xsl:template match="order">
    <message>                                    <!-- ✅ Can debug here (line 12) -->
        <xsl:attribute name="orderId">          <!-- ❌ Cannot debug here -->
            <xsl:value-of select="@id"/>         <!-- ❌ Cannot debug here -->
        </xsl:attribute>                         <!-- ❌ Cannot debug here -->
        <xsl:attribute name="status">           <!-- ❌ Cannot debug here -->
            <xsl:text>urgent</xsl:text>          <!-- ❌ Cannot debug here -->
        </xsl:attribute>                         <!-- ❌ Closing tag - no code to debug -->
        <content>                                <!-- ✅ Can debug here (line 25) -->
            <xsl:value-of select="item"/>        <!-- ✅ Can debug here -->
        </content>
    </message>
</xsl:template>
```

### Observed Behavior

When stepping through code:
- Debugger stops **before** the first `xsl:attribute` (e.g., line 12)
- Attributes are processed without stopping
- Debugger resumes **after** the last `xsl:attribute` (e.g., line 25)
- **Closing tags** (like `</xsl:attribute>`) are skipped - they contain no executable code

### Workarounds

To debug attribute values:

1. **Set breakpoint before attributes**
   ```xml
   <message>  <!-- Set breakpoint here to inspect context -->
       <xsl:attribute name="id">
           <xsl:value-of select="@id"/>
       </xsl:attribute>
   ```

2. **Set breakpoint after attributes**
   ```xml
       </xsl:attribute>
       <content>  <!-- Set breakpoint here to verify attributes were set -->
   ```

3. **Use xsl:message to output values**
   ```xml
   <xsl:message>DEBUG: orderId = <xsl:value-of select="@id"/></xsl:message>
   <xsl:attribute name="orderId">
       <xsl:value-of select="@id"/>
   </xsl:attribute>
   ```

4. **Check output XML** to verify attribute values are correct

---

## 2. Closing Tags Cannot Be Debugged

### Cannot Set Breakpoints on Closing Tags

**Limitation:** Closing tags like `</message>`, `</xsl:attribute>`, `</xsl:template>` cannot have breakpoints.

**Reason:** Closing tags contain no executable code - they simply mark the end of an element.

### Example

```xml
<xsl:template match="/">
    <root>                          <!-- ✅ Can debug -->
        <xsl:value-of select="@id"/> <!-- ✅ Can debug -->
    </root>                          <!-- ❌ Closing tag - nothing to debug -->
</xsl:template>                      <!-- ❌ Closing tag - nothing to debug -->
```

### What This Means

- Debugger will skip over closing tags
- Last executable line in a block is where debugger stops
- This is **expected behavior**, not a bug

---

## 3. Other Non-Debuggable Elements

The following XSLT elements are excluded from debugging:

### Top-Level Declarations (No Executable Code)
- `xsl:stylesheet` / `xsl:transform`
- `xsl:output`
- `xsl:import` / `xsl:include`
- `xsl:key`
- `xsl:decimal-format`
- `xsl:namespace-alias`
- `xsl:attribute-set`
- `xsl:preserve-space` / `xsl:strip-space`

### Elements That Don't Contain Debuggable Code
- `xsl:param` / `xsl:variable` declarations (definitions only)
- `xsl:with-param` (parameter passing)
- `xsl:sort` (sorting criteria)
- `xsl:message` content (by design - prevents recursive debugging)

### Example

```xml
<!-- ❌ Cannot debug top-level declarations -->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
    <xsl:output method="xml" indent="yes"/>

    <!-- ❌ Cannot debug parameter declarations -->
    <xsl:param name="inputParam" select="'default'"/>

    <!-- ✅ Can debug template content -->
    <xsl:template match="/">
        <root>
            <!-- ✅ Can debug executable instructions -->
            <xsl:value-of select="$inputParam"/>
        </root>
    </xsl:template>
</xsl:stylesheet>
```

---

## 4. Why These Limitations Exist

### Technical Reasons

1. **XSLT Specification Compliance**
   - Some elements can only contain specific child elements
   - Adding debug instrumentation would violate the spec

2. **Output Correctness**
   - Instrumenting inside attributes could corrupt output
   - Debug calls might appear in generated text

3. **Performance**
   - Not instrumenting declarations avoids unnecessary overhead
   - Focuses debugging on executable code

### Design Philosophy

The debugger follows the principle:
> **Only instrument executable XSLT instructions that produce output or side effects**

Non-executable declarations, closing tags, and constrained elements are intentionally excluded.

---

## 5. How to Work With These Limitations

### General Debugging Strategy

1. **Set breakpoints on executable lines**
   - Template entry points
   - `xsl:value-of`, `xsl:apply-templates`, `xsl:call-template`
   - Control flow: `xsl:if`, `xsl:choose`, `xsl:for-each`

2. **Use xsl:message for diagnostic output**
   ```xml
   <xsl:message>
       Current node: <xsl:value-of select="name()"/>
       Value: <xsl:value-of select="."/>
   </xsl:message>
   ```

3. **Check variables before/after attribute blocks**
   ```xml
   <xsl:variable name="orderId" select="@id"/>  <!-- ✅ Debug here -->
   <xsl:attribute name="orderId">
       <xsl:value-of select="$orderId"/>
   </xsl:attribute>
   <!-- Check $orderId value at breakpoint above -->
   ```

4. **Verify output XML** to confirm attribute values

---

## 6. What Works Perfectly

### Fully Supported Debugging

✅ **Template execution**
- Entry/exit of named templates
- Template matching

✅ **Control flow**
- `xsl:if` conditions
- `xsl:choose` / `xsl:when` / `xsl:otherwise`
- `xsl:for-each` iterations

✅ **Variable capture**
- Local variables in templates
- Variable values at breakpoints

✅ **Content generation**
- `xsl:value-of`
- `xsl:text`
- Literal result elements

✅ **Function calls**
- `xsl:call-template`
- `xsl:apply-templates`
- Extension functions

---

## 7. Summary Table

| Element Type | Can Debug? | Notes |
|--------------|-----------|-------|
| `xsl:template` body | ✅ Yes | Full debugging support |
| `xsl:if` / `xsl:choose` | ✅ Yes | Can step through branches |
| `xsl:for-each` | ✅ Yes | Can step through iterations |
| `xsl:value-of` | ✅ Yes | Can see selected values |
| `xsl:variable` | ✅ Yes* | Can capture values (*not inside attributes) |
| `xsl:attribute` | ❌ No | Skipped by design |
| Content inside `xsl:attribute` | ❌ No | Specification constraint |
| Closing tags | ❌ No | No executable code |
| Top-level declarations | ❌ No | Not executable |
| `xsl:message` | ⚠️  Partial | Message displayed, but not debugged |

---

## 8. Related Documentation

- **AttributeInstrumentation-CodeReview.md** - Technical details of attribute instrumentation
- **Comprehensive-CodeReview.md** - Full codebase analysis
- **Test Coverage** - See `AttributeInstrumentationTests.cs` for examples

---

## Questions?

If you encounter unexpected behavior not described here, please check:
1. Is the transformation producing correct output?
2. Are attributes being set correctly in the output XML?
3. Can you set breakpoints before/after the problematic section?

If issues persist, file a bug report with:
- Sample XSLT file
- Expected vs actual behavior
- Output XML showing the problem
