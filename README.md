## Design Considerations & Assumptions

### Architecture & Design
* **Architecture:** Extracted core logic into services, used Dependency Inversion via interfaces (`IPaymentService`, `IPaymentsRepository`), and separated Domain Entities from API DTOs to keep controllers thin and highly testable.
* **Validation:** Used ASP.NET Core Data Annotations (`[Range]`, `[RegularExpression]`) for simple field validation. Suppressed default `ModelStateInvalidFilter` to map 400 errors directly to the `{ "status": "Rejected" }` payload requested by the spec.
* **Typed HTTP Client:** Used `IHttpClientFactory` to configure and inject `BankSimulatorClient`, ensuring correct handling of the `HttpClient` lifecycle and avoiding socket exhaustion.

### Trade-offs & Assumptions (Given Constraints)
* **Storage Thread-Safety:** Used a basic in-memory `List<Payment>` repository as a test double to meet constraints without over-engineering. In production, this would be backed by a proper database (e.g. via EF Core) or at least `ConcurrentDictionary` to prevent multithreaded race conditions.
* **Resilience:** Omitted circuit breakers, retries (e.g. Polly), or asynchronous queuing architectures for bank simulator calls to optimize for code simplicity based on the brief.
* **PCI Compliance:** Assumed plain-text submission of card networks (PANs) is acceptable purely for this exercise. Real-world implementations would rely on tokenized inputs via iFrames (e.g. Checkout.com Frames) to prevent raw PAN ingestion.
* **Date Validation:** Hardcoded `2024` as the minimum array constraint for `ExpiryYear` in data annotations for simplicity, assuming the current year. Custom validation logic would be required for dynamic year tracking.