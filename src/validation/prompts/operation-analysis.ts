export const OPERATION_ANALYSIS = `## Analyzing Code Changes

### For Edit/MultiEdit Operations
When reviewing edits, you must:

1. **Identify truly NEW content**
   - Compare old string vs new string character by character
   - A test is NEW only if it doesn't exist in the old string
   - Modifying an existing test is not adding a new test
   - Moving or reformatting tests doesn't count as new

2. **Count new tests accurately**
   - Look for test blocks: \`test(\`, \`it(\`, \`describe(\`, \`[Fact]\`, \`[Theory]\`
   - Count only tests that appear in new but not in old
   - One new test = allowed, Multiple new tests = violation

3. **Check implementation changes**
   - Determine what the test failure indicates
   - Verify implementation matches the failure type
   - Ensure minimal implementation principle. It is allowed to implement downstream changes without requiring a separate smaller TDD cycle.

### For Write Operations
When reviewing new file creation:

1. **Test files**: Usually allowed (starting TDD cycle)
   - Should contain only one test initially
   - Multiple tests in new file = violation

2. **Implementation files**: Check context carefully
   - Need evidence of failing test
   - Implementation must match test failure type directly or indirectly.

### Context Clues to Consider

1. **Test Output**: Most important indicator
   - Shows current TDD phase
   - Reveals what implementation is needed
   - Missing output = likely violation

2. **Todo List**: Provides intent
   - "Refactor" todos require passing tests
   - "Add test" todos support test creation
   - Multiple "add test" todos don't justify multiple tests

3. **File Paths**: Indicate test vs implementation
   - Changes must be appropriate for file type
   - Test changes have different rules than implementation`
