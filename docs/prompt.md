Run the following propmt in "plan mode" for Claude Sonnet 4.6 High in VS Code. This will generate a plan for implementing the money transfer console app based on the provided requirements:

We're going to build a .Net 10 / C# 14 Money Transfer console app.

These are the customer's requirements:

```
- Transfer represents a transfer of a positive amount of money in a given currency
- Currency is represented according to ISO-4217
- Money amounts are strictly decimal values
- Each money transfer has a GUID public ID
- Each money transfer is performed between two accounts identified by their GUID public IDs
- Transfer can be pending, approved (partly or fully), executed, expired, or rejected
- An executed transfer has a UTC timestamp of its execution
- A transfer has a UTC timestamp defining its expiration
- Any transfer that has not been executed by the expiration UTC timestamp will expire automatically
- A transfer may require approval by two employees, or may be auto-approved
- The outer caller determines whether the transfer is auto-approved or requires approval
- A transfer that requires approval can receive approval from one employee, after which it becomes partly approved
- A partly approved transfer becomes approved after receiving approval from a second employee, which is different from the first one
- Employees are identified by their GUID IDs
- Approved/auto-approved transfer can be executed, if execution time is before the timestamp by which it must have been executed
- Pending/partly approved transfer may be rejected by an employee, and the GUID identity of the employee is stored in it
```

Implement a model for the money transfer in the Models namespace.

Do an Ultrathink and explore all design patterns to identify the best pattern to use. Keep in mind:

1. No exception throwing.
2. Validation is done before business logic.
3. Use FluentValidation.
4. Do not use a state machine!
5. Use less verbose syntax.
6. Use DRY (3 or more occurrences), KISS, SOLID!
7. Use fluent or builder - whichever makes sense.
8. Keep the DI registration in the program.cs and add a DI DemoRunner that runs five scenarios in the demo.
9. Add Unit and integration tests.
10. We're using DI, so wire it up.
11. Add Architectural, Use Cases, Flow documentation in the docs folder. Use Mermaid diagrams for flows and sequence diagrams for use cases.
12. Add a README with instructions to run the app and tests.
