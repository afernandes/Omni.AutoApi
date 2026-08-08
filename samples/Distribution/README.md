# Distribuindo o cliente gerado para outra solution

Como usar o cliente HTTP gerado pelo Omni.AutoApi num projeto que **não** está na mesma solution.

> ⚠️ **Este exemplo não compila num clone novo sem um passo prévio.** O `Consumer` consome
> `MyApi.Contracts` como **pacote** (é justamente o que o exemplo demonstra), e o feed local é
> artefato de build — está no `.gitignore`. Gere-o primeiro:
>
> ```powershell
> ./samples/Distribution/build.ps1
> ```
>
> O script empacota `Omni.AutoApi.Abstractions` + `MyApi.Contracts` em `_feed/` e compila o
> `Consumer`. Por isso este exemplo também fica fora de `Omni.AutoApi.sln` e do CI.

## A regra que decide tudo

Um **source generator só enxerga código-fonte da compilação atual** — ele **não** gera clientes
para interfaces que vêm de um **DLL/pacote referenciado**. Logo:

> O gerador precisa rodar **no assembly onde a interface `[AutoApiClient]` é declarada como código**.

Daí a abordagem recomendada: coloque a interface numa **lib de contratos** que referencia o gerador;
o cliente é **compilado dentro** dela; distribua a lib (com o cliente assado) como pacote.

## Abordagem recomendada — pacote de contratos (verificada neste exemplo)

```
MyApi.Contracts/   # interface [AutoApiClient] + DTOs + gerador (analyzer) -> client assado no DLL
Consumer/          # OUTRA solution: referencia só o pacote MyApi.Contracts
_feed/             # feed NuGet local (em produção: feed privado ou nuget.org)
```

**1) A lib de contratos** referencia o gerador como analyzer com `PrivateAssets="all"` (o consumidor
**não** precisa do gerador) e o `Omni.AutoApi.Abstractions` normalmente (flui como dependência). Para que
as **extensões de DI** (`AddAllAutoApiClients`, `Add{Nome}Client`) também sejam empacotadas, a lib de
contratos precisa referenciar `Microsoft.Extensions.DependencyInjection.Abstractions` — o gerador só
as emite quando ela está presente **na compilação dos contratos**:

```xml
<ProjectReference Include="...\Omni.AutoApi.Abstractions\Omni.AutoApi.Abstractions.csproj" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="9.0.0" />
<ProjectReference Include="...\Omni.AutoApi.Client.SourceGenerator\Omni.AutoApi.Client.SourceGenerator.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" PrivateAssets="all" />
```

**2) Empacote** os contratos + suas dependências Omni.AutoApi num feed:

```bash
dotnet pack src/Omni.AutoApi.Abstractions          -c Release -o samples/Distribution/_feed
dotnet pack samples/Distribution/MyApi.Contracts -c Release -o samples/Distribution/_feed
```

O `MyApi.Contracts.nuspec` resultante depende de `Omni.AutoApi.Abstractions` **mas não do gerador**.

**3) O consumidor** (outra solution) só adiciona o feed (`nuget.config`) e referencia o pacote:

```xml
<PackageReference Include="MyApi.Contracts" Version="1.0.0" />
```
```csharp
using MyApi.Contracts;                 // o client veio assado no pacote
// (a) instanciação direta (não precisa de DI):
var client = new TodoAppServiceClient(new HttpClient { BaseAddress = new Uri(serverUrl) });
var todos  = await client.GetTodosAsync();

// (b) via DI (extensão gerada/empacotada nos contratos; o consumidor referencia
//     Microsoft.Extensions.DependencyInjection):
using Omni.AutoApi.Client.Generated;
services.AddAllAutoApiClients((_, http) => http.BaseAddress = new Uri(serverUrl));
```

✅ Verificado (ambos os caminhos): o `Consumer` builda e chama o servidor real **sem o gerador e sem o
código-fonte da interface** — apenas o pacote.

## Gotcha importante: cache global do NuGet

Ao **republicar a MESMA versão** (`1.0.0`) com mudanças, o consumidor pode pegar o DLL antigo do
cache global (`~/.nuget/packages`). **Sempre bump da versão** (`1.0.1`, ...) ao mudar o contrato,
ou limpe o cache (`dotnet nuget locals global-packages --clear`) em dev.

## Alternativas

- **ProjectReference cross-solution**: o consumidor pode referenciar o `.csproj` de contratos por
  caminho relativo (sem empacotar). Bom para monorepo; o cliente vem assado igual.
- **Consumidor gera o próprio cliente**: só funciona se a interface estiver como **fonte** na
  compilação do consumidor (shared source / `<Compile Include>` linkado / pacote *source-only*).
  Referenciar a interface por **DLL** não dispara o gerador.
- **Proxy dinâmico** (`Omni.AutoApi.Client` + `DynamicHttpProxy<T>`): sem geração; o consumidor referencia
  `Omni.AutoApi.Client` + a interface e faz `AddAutoApiClient<ITodoAppService>(...)`. Útil quando não dá
  para rodar o gerador na lib de contratos.

## Produção

- **Feed privado**: Azure Artifacts, GitHub Packages, MyGet — configure auth no `nuget.config`.
- **SemVer + sincronia de contrato**: versione os contratos quando a API evolui (breaking → major) e
  **publique a versão de contratos junto** com a do servidor. Não há validação de assinatura em runtime.
- **DTOs públicos**: parâmetros complexos em GET/DELETE viram query via reflexão sobre **propriedades
  públicas** — DTOs `internal`/props não-públicas são ignorados. Prefira DTOs públicos.
- **Trimming/Native AOT**: o helper gerado já marca o caminho de reflexão com
  `[DynamicallyAccessedMembers(PublicProperties)]`. Ainda assim, em apps trimmed/AOT prefira parâmetros
  simples ou body (evita o caminho reflexivo de GET-complexo).
- **Ambiente de build dos contratos**: o gerador exige Roslyn compatível com o SDK; fixe a versão do
  .NET SDK (global.json) no projeto/CI que **produz** o pacote de contratos.
