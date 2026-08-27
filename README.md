# Enterprise Credit & Reconciliation Engine (ECRE)

Un motor de backend de nivel corporativo diseñado para la administración del ciclo de vida de créditos, cálculo de tablas de amortización y registro contable inmutable bajo principios de partida doble y Clean Architecture.

### Puntos clave de ingeniería
- **Dominio aislado:** Lógica de negocio pura sin dependencias de infraestructura ni frameworks web.
- **Libro Mayor Inmutable:** Registro de transacciones con balance estricto $(\sum \text{Débitos} - \sum \text{Créditos} = 0)$.
- **QA & Testing:** Suite de pruebas unitarias exhaustivas con FluentAssertions y medición de cobertura vía Coverlet.
