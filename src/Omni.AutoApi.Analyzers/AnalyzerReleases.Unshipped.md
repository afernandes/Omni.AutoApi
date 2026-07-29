; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
AUTOAPI002 | Omni.AutoApi | Warning | Sobrecarga (ou par Foo/FooAsync) gera rota duplicada — falha no startup
AUTOAPI003 | Omni.AutoApi | Warning | Mais de um parâmetro complexo em verbo com corpo — o MVC só aceita um [FromBody]
AUTOAPI004 | Omni.AutoApi | Info | Método não assíncrono não pode ser consumido pelos clientes tipados
