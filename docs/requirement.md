# Qwiik Technical Assessment (Hands-on Engineering Leader)

Qwiik is a SaaS platform used by companies in the logistics, shipping, finance, and invoice workflow space.

We are hiring a hands-on Head of Engineering. This role requires strong software engineering ability, practical architecture judgment, production ownership, and leadership maturity.

This assessment is designed to evaluate how you think, design, build, and explain a small production-minded system when requirements are intentionally not over-specified.

You may use AI coding assistants, but you must clearly disclose how you used them.

---

### Task

Build a small backend API for a multi-tenant invoice management module.

The API should allow a SaaS customer to manage invoices for their organization.

At minimum, the system should allow users to:

1. **Create an invoice**
2. **List invoices**
3. **View invoice details**
4. **Update invoice status**
5. **Retrieve a basic invoice summary/dashboard**

You are free to decide:

* The domain model
* The required fields
* The API shape
* The database design
* The validations
* The status lifecycle
* The response structure
* The project structure
* The indexing strategy
* The testing approach
* The assumptions and limitations

We are intentionally not giving detailed field-level requirements. We want to see how you think as an engineering leader.

---

### Required Technology

**Use:**
* C#
* ASP.NET Core Web API
* Entity Framework Core
* SQL Server / LocalDB / SQL Server Docker image
* Git

**Optional but welcome:**
* Swagger / OpenAPI
* Structured logging
* FluentValidation or similar
* Docker Compose
* Basic CI pipeline
* Integration tests
* Azure-ready configuration

---

### Expectations

We are not expecting a large enterprise system. We are expecting a small, clean, thoughtful, production-minded implementation.

Your solution should demonstrate:

1. Good C# and ASP.NET Core practices
2. Sensible API design
3. Practical database modelling
4. Multi-tenant SaaS awareness
5. SQL Server / EF Core understanding
6. Validation and error handling
7. Query efficiency and pagination
8. Basic security thinking
9. Testing of important business rules
10. Clear documentation and trade-off explanation

You do not need to solve every possible problem, but you should show awareness of the important ones.

---

### AI Usage

You may use AI tools such as ChatGPT, Claude, GitHub Copilot, Cursor, or similar.

Create a file named `AI_USAGE.md` and explain:

1. Which AI tools you used
2. What you used AI for
3. What you personally reviewed
4. What AI got wrong, if anything
5. What parts you wrote, corrected, or significantly changed yourself

We are not judging whether you used AI. We are judging whether you can use AI responsibly as an engineering leader.

---

### Documentation Required

Create a file named `SOLUTION_NOTES.md` and include:

1. How to run the project
2. Your assumptions
3. Architecture overview
4. Domain model explanation
5. Database design explanation
6. API design explanation
7. Validation approach
8. Tenant isolation approach
9. Indexing and performance strategy
10. Testing approach
11. Azure deployment and monitoring considerations
12. Security considerations
13. Known limitations
14. What you would improve with more time

#### Azure Production Thinking
In your documentation, explain how you would deploy this API to Azure, services you would use and why.

We would appreciate any further details from your side (not required) about best practices, scaling, rollbacks, and resource utilisation.

---

### Submission

Submit one of the following:
* GitHub repository link
* ZIP file

Your submission should include:

1. Source code
2. Database migration or SQL script
3. `README.md`
4. `SOLUTION_NOTES.md`
5. `AI_USAGE.md`
6. Tests, if included

Please spend no more than **5–6 hours**.

---

*We are not looking for a perfect system. We are looking for engineering judgment, clean execution, practical architecture, and production awareness.*