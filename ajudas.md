# Tutorial 2 — ASP.NET Core MVC  
## Formulários, GET/POST, Model Binding e Validação

---

## Objetivo do Tutorial

O Tutorial 2 tem como objetivo compreender **como funcionam os formulários em ASP.NET Core MVC**, desde a abordagem mais simples até uma abordagem mais correta e profissional baseada em **Model Binding** e **DataAnnotations**.

Ao longo do tutorial são explorados:

- a diferença entre **GET** e **POST**;
- como o MVC recebe dados de formulários;
- como transferir dados entre Controller e View;
- como validar dados manualmente;
- como validar dados automaticamente usando o **Model**;
- o padrão **Post → Redirect → Get (PRG)**.

---

## Parte 1 — Formulário simples (sem Model)

### Objetivo

Perceber:
- como um formulário HTML envia dados;
- como o MVC distingue pedidos GET e POST;
- como usar `IFormCollection` e `ViewData`.

---

### Controller: `ExampleController`

#### Ação GET

```csharp
public IActionResult Index()
{
    return View();
}
```

**Teoria:**

- Um pedido **GET** é usado para pedir uma página.
- Este método é chamado quando o utilizador acede a `/Example/Index`.
- `return View()` renderiza a view `Views/Example/Index.cshtml`.

---

### View: `Views/Example/Index.cshtml`

```cshtml
@{
    ViewData["Title"] = "Index";
}

<h1>Index</h1>

<form method="post" action="">
    <label>Name</label>
    <input type="text" name="name" />

    <label>Age</label>
    <input type="text" name="age" />

    <input type="submit" value="let's go submit this" />
</form>
```

**Teoria:**

- `method="post"` indica que o formulário será enviado por **POST**.
- `action=""` significa que o POST é enviado para a **mesma ação** (`Index`).
- Os atributos `name="name"` e `name="age"`:
  - são as chaves usadas pelo servidor para ler os valores.

---

### Ação POST

```csharp
[HttpPost]
public IActionResult Index(IFormCollection formData)
{
    ViewData["text_inserted"] = formData["name"];
    ViewData["other_text_inserted"] = formData["age"];

    return View("Index2");
}
```

**Teoria:**

- `[HttpPost]` indica que este método responde apenas a pedidos POST.
- `IFormCollection` é uma **coleção de pares chave–valor**.
- Todos os valores chegam como **string**.
- `ViewData` é um **Dictionary** usado para enviar dados do Controller para a View.
- `return View("Index2")` força o uso da view `Index2.cshtml`.

---

### View de resultado: `Views/Example/Index2.cshtml`

```cshtml
@{
    ViewData["Title"] = "Index2";
}

<h1>Index2</h1>

Data inserted in FORM
<br />
Name field: @ViewData["text_inserted"]<br />
Age field: @ViewData["other_text_inserted"]
```

**Teoria:**

- `@ViewData["..."]` permite aceder aos valores definidos no Controller.
- O símbolo `@` indica código Razor (C# misturado com HTML).

---

## Parte 2 — Formulário gerado a partir de um Model (`Person`)

### Objetivo

- Criar um formulário a partir de uma classe Model;
- Introduzir validação manual;
- Usar `TempData` e `RedirectToAction`.

---

### Model: `Person.cs`

```csharp
public class Person
{
    public string? Name { get; set; }
    public int Age { get; set; }
}
```

**Teoria:**

- O Model representa os **dados da aplicação**.
- Cada propriedade corresponde a um campo do formulário.

---

### Scaffold do `PersonController`

O scaffolding gera automaticamente um controller com operações **CRUD**:

- Create  
- Retrieve  
- Update  
- Delete  

Neste tutorial **usa-se apenas a ação Create**.

---

### View: `Views/Person/Create.cshtml`

```cshtml
@model Aula02.Models.Person

<form asp-action="Create">
    <label asp-for="Name"></label>
    <input asp-for="Name" />
    <span asp-validation-for="Name"></span>

    <label asp-for="Age"></label>
    <input asp-for="Age" />
    <span asp-validation-for="Age"></span>

    <input type="submit" value="Create" />
</form>
```

**Teoria:**

- `@model Person` liga a View ao Model.
- `asp-for` gera automaticamente:
  - `name`, `id` e valor do input.
- `asp-validation-for` mostra mensagens de erro associadas ao campo.

---

### Validação manual no Controller

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult Create(IFormCollection collection)
{
    if (string.IsNullOrEmpty(collection["name"]))
        ModelState.AddModelError("name", "Mandatory field");

    if (string.IsNullOrEmpty(collection["age"]))
        ModelState.AddModelError("age", "Mandatory field");
    else
    {
        try
        {
            int age = int.Parse(collection["age"]);
            if (age < 18 || age > 100)
                ModelState.AddModelError("age", "Age should be between 18 and 100");
        }
        catch
        {
            ModelState.AddModelError("age", "Must indicate an integer number");
        }
    }

    if (ModelState.IsValid)
    {
        TempData["values"] = collection["name"] + " [" + collection["age"] + "]";
        return RedirectToAction(nameof(Index));
    }

    return View();
}
```

**Teoria:**

- `ModelState` guarda erros de validação.
- `ModelState.AddModelError` associa erros a campos específicos.
- `ModelState.IsValid` indica se existem erros.
- `TempData` permite passar dados **entre ações**.
- `RedirectToAction` implementa o padrão **Post → Redirect → Get (PRG)**.

---

### View de resultado: `Views/Person/Index.cshtml`

```cshtml
<h1>Formulary submission</h1>

Submitted with SUCCESS!!!
<br />
It was inserted the following data: @TempData["values"]
```

**Teoria:**

- `TempData` só existe até ser lido.
- Ideal para mensagens de confirmação após um POST.

---

## Parte 3 — Validação automática com DataAnnotations

### Objetivo

- Remover validação manual;
- Centralizar regras no Model;
- Usar Model Binding.

---

### Model `Person` com DataAnnotations

```csharp
public class Person
{
    [DisplayName("Full name")]
    [Required(ErrorMessage = "The field {0} is mandatory")]
    public string? Name { get; set; }

    [DisplayName("Age")]
    [Required]
    [Range(18, 100, ErrorMessage = "{0} must be between {1} and {2}")]
    public int Age { get; set; }

    [DisplayName("Appointment date")]
    [DataType(DataType.DateTime)]
    public DateTime AppointmentDate { get; set; }

    [DisplayName("Email address")]
    [DataType(DataType.EmailAddress)]
    public string? Email { get; set; }
}
```

**Teoria:**

- `[Required]` → campo obrigatório.
- `[Range]` → intervalo permitido.
- `[DisplayName]` → texto mais amigável nos labels.
- `[DataType]`:
  - `DateTime` → gera input com calendário (HTML5).
  - `EmailAddress` → valida formato de email.

---

### Novo Create no Controller

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public ActionResult Create(Person newPerson)
{
    if (ModelState.IsValid)
    {
        TempData["values"] = newPerson.Name + " [" + newPerson.Age + "]";
        return RedirectToAction(nameof(Index));
    }

    return View();
}
```

**Teoria:**

- O MVC faz **Model Binding**:
  - cria um objeto `Person`;
  - converte strings para os tipos corretos;
  - aplica automaticamente as DataAnnotations.
- O programador apenas verifica `ModelState.IsValid`.

---

## Conceitos-chave aprendidos

- GET vs POST  
- IFormCollection vs Model Binding  
- ViewData vs TempData  
- ModelState e validação  
- DataAnnotations  
- Post → Redirect → Get (PRG)  
- Separação correta entre:
  - Model
  - View
  - Controller


--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------


# 🌐 Tutorial 3 – ASP.NET Core MVC + Entity Framework Core (Code First)

> Resumo organizado para estudo no Visual Studio (Markdown puro, sem HTML).

---

## 🚀 Objetivo Geral do Tutorial

- Criar uma aplicação web **ASP.NET Core MVC**
- Gerir dados usando operações **CRUD**
- Guardar informação numa base de dados **SQL Server LocalDB**
- Usar **Entity Framework Core (EF Core)**
- Aplicar a abordagem **Code First**

---

## 🟥 1. O que é uma *Entity Model*?

Uma *entity model* é uma classe C# que representa uma tabela da base de dados.

**Exemplo:**

- Classe C#: `Category`
- Tabela SQL: `Category`

---

## 🟦 2. O que é CRUD?

**CRUD** representa as operações básicas sobre dados:

- **Create** → Criar registos (INSERT)
- **Read** → Ler / Listar (SELECT)
- **Update** → Atualizar (UPDATE)
- **Delete** → Apagar (DELETE)

No ASP.NET Core MVC + EF Core:

- As operações CRUD são feitas através de **controllers** e **views**
- O EF Core gera o SQL automaticamente → não é preciso escrever queries à mão

---

## 🟩 3. Entity Framework Core (EF Core)

O **Entity Framework Core** é uma biblioteca da Microsoft que:

- Liga a aplicação C# à base de dados
- Mapeia **classes** para **tabelas**
- Mapeia **propriedades** para **colunas**
- Gera automaticamente as queries SQL necessárias

É um **ORM (Object-Relational Mapper)**.

---

## 🟪 4. Code First vs Database First

### Database First

- Começas pela base de dados
- Gera-se o código C# a partir da BD

### Code First (usado neste tutorial)

1. Criar classes C# (modelos)  
2. Decorar as propriedades com atributos, por exemplo:  
   - `[Required]`
   - `[StringLength(50)]`
3. Executar `add-migration`  
   - Cria um ficheiro com o “plano” da BD (migration)
4. Executar `update-database`  
   - Cria ou atualiza a BD de acordo com o modelo

**Conclusão:**  
No Code First, o **código controla a estrutura da base de dados**.

---

## 🟧 5. Componentes essenciais do EF Core

Uma aplicação com EF Core precisa de três componentes principais:

### 1. Data Context (DbContext)

É uma classe que representa a **ligação à BD** e contém os conjuntos de dados (tabelas):

```csharp
public class Class03Context : DbContext
{
    public DbSet<Category> Category { get; set; }
}
```

Aqui:

- `Class03Context` é o contexto da BD
- `DbSet<Category>` representa a tabela `Category`

---

### 2. Connection String

A **connection string** fica no ficheiro `appsettings.json` e indica:

- Servidor SQL
- Nome da base de dados
- Método de autenticação

Exemplo:

```json
"ConnectionStrings": {
  "Class03Context": "Server=(localdb)\mssqllocaldb;Database=Class03;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

Sem esta string, o EF Core **não sabe onde criar / ligar-se à BD**.

---

### 3. Registo do DbContext no Program.cs

No `Program.cs`, o contexto é registado como um serviço:

```csharp
builder.Services.AddDbContext<Class03Context>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Class03Context")));
```

Isto:

- Lê a connection string do `appsettings.json`
- Configura o EF Core para usar SQL Server
- Permite que o `Class03Context` seja injetado nos controllers (Dependency Injection)

---

## 🟨 6. O que é uma Migration?

**Definição simples:**

> Uma *migration* é um ficheiro gerado pelo EF Core que descreve como criar ou alterar a base de dados de acordo com as tuas classes C#.

Metáfora:

- As classes C# (modelos) → são a planta da casa
- A migration → é o plano de obra (o que construir, alterar, remover)
- O comando `update-database` → é o pedreiro a executar a obra

---

## 🟫 7. Comando `add-migration FirstStep`

Quando executas:

```powershell
PM> add-migration FirstStep
```

O EF Core:

- Lê as tuas classes do modelo (ex: `Category`)
- Compara com o estado anterior (ou vazio, se for a primeira)
- Gera uma nova migration chamada **FirstStep**

Na pasta `Migrations`, surgem ficheiros como:

- `2023xxxxxxxx_FirstStep.cs` → ficheiro da migration  
- `Class03ContextModelSnapshot.cs` → snapshot do estado atual do modelo

### Diferença entre Migration e Snapshot

- **Migration (FirstStep.cs)**  
  - Descreve **o que mudar na BD**
  - Tem métodos:
    - `Up()` → aplicar alterações
    - `Down()` → desfazer alterações

- **Snapshot (Class03ContextModelSnapshot.cs)**  
  - Guarda uma **fotografia do modelo atual**
  - Quando crias uma nova migration, o EF compara:
    - Classes C# atuais
    - Snapshot anterior  
  - E descobre o que mudou

---

## 🟦 8. Comando `update-database`

Quando executas:

```powershell
PM> update-database
```

O EF Core:

- Lê todas as migrations que ainda não foram aplicadas
- Executa as operações necessárias (como `CREATE TABLE`, `ALTER TABLE`, etc.)
- Cria a base de dados, se ainda não existir
- Cria também a tabela interna `__EFMigrationsHistory`, para registar as migrations aplicadas

No teu caso:

- Foi criada a BD
- Foi criada a tabela `Category` conforme definido na migration `FirstStep`

---

## 🟩 9. DbInitializer – Seed Data (Inicializar a BD com dados)

Depois de criar a base de dados, ela está vazia.  
Para facilitar testes, criamos um **DbInitializer** que:

1. Garante que a BD existe:

   ```csharp
   _context.Database.EnsureCreated();
   ```

2. Verifica se a tabela `Category` já tem dados:

   ```csharp
   if (_context.Category.Any())
   {
       return; // Já tem dados, não faz nada
   }
   ```

3. Se estiver vazia, insere dados de teste:

   ```csharp
   var categorias = new Category[]
   {
       new Category { Name = "Programming", Description = "Algorithms and programming area courses" },
       new Category { Name = "Administration", Description = "Public administration and business management courses" },
       new Category { Name = "Communication", Description = "Business and institutional communication courses" }
   };

   _context.Category.AddRange(categorias);
   _context.SaveChanges();
   ```

---

## 🟥 10. Ligar o DbInitializer ao arranque da aplicação (Program.cs)

Passos:

1. **Registar o DbInitializer como serviço**

   ```csharp
   builder.Services.AddTransient<DbInitializer>();
   ```

2. **Criar um scope e obter o serviço**

   ```csharp
   using var scope = app.Services.CreateScope();
   var services = scope.ServiceProvider;
   var initializer = services.GetRequiredService<DbInitializer>();
   initializer.Run();
   ```

Explicação:

- O `DbInitializer` é criado através de **Dependency Injection**
- O método `Run()` é chamado no arranque da aplicação
- Isso garante que a BD é criada (se necessário) e preenchida com dados de teste apenas uma vez

---

## 🟦 11. Ciclo de vida dos serviços (Service Lifetime)

No ASP.NET Core, os serviços podem ter três tipos de *lifetime*:

| Lifetime      | Quando cria nova instância?    | Usado para                   |
|---------------|--------------------------------|------------------------------|
| **Transient** | Sempre que o serviço é pedido | Serviços simples, sem estado |
| **Scoped**    | Uma vez por pedido HTTP       | `DbContext`                  |
| **Singleton** | Uma vez para toda a aplicação | Configurações globais        |

### Porque é que o DbInitializer é Transient?

- Não guarda estado
- É usado pontualmente
- É barato de criar
- Não precisa ser reutilizado

---

## 🟪 12. O que é um Scope?

Um **scope** é um “ambiente” temporário onde serviços do tipo **Scoped** (como o `DbContext`) podem ser criados e usados.

- Em cada pedido HTTP, o ASP.NET Core cria automaticamente um scope.
- No `Program.cs`, não estamos dentro de um pedido HTTP, por isso é necessário criar um scope manualmente para poder usar o `DbContext` e o `DbInitializer`.

Exemplo:

```csharp
using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
var initializer = services.GetRequiredService<DbInitializer>();
initializer.Run();
```

---

## ✅ Resumo Final

Este tutorial cobre:

- Conceitos de **Entity Model** e **CRUD**
- Funcionamento do **Entity Framework Core**
- Abordagem **Code First**
- Como funcionam **migrations** e **update-database**
- Como inicializar a BD com dados de teste usando **DbInitializer**
- Como funcionam **Dependency Injection**, **Service Lifetime** e **Scopes**



--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------


---------------------------------------------------------------------------------------------
Tutorial 4(continuação do tutorial3) explicação do tutorial 4
---------------------------------------------------------------------------------------------

O objetivo deste Tutorial 4 é evoluir a aplicação do Tutorial 3 de “CRUD simples” (tabelas independentes) 
para um projeto com **modelo relacional** (tabelas ligadas entre si), mantendo a mesma ideia:
fazer CRUD sem escrever SQL, porque quem trata da base de dados é o **Entity Framework Core** através de **Code First**.

Em termos práticos, vais aprender/obter isto:

- Adicionar uma nova entidade (**Course**) ao projeto.
- Criar uma relação entre entidades (relacional):
  - Um **Course pertence a uma Category** (Foreign Key).
  - Uma **Category pode ter vários Courses** (relação 1-para-N).

- Fazer o EF Core gerar automaticamente:
  - a nova tabela `Course`
  - a chave estrangeira (FK) para `Category`
  - os ajustes necessários, tudo via **Migrations (Code First)**

- Criar automaticamente (**scaffolding**) o controller e views para CRUD de `Course` usando EF Core:
  - listar, criar, editar, ver detalhes, apagar cursos
  - sem SQL manual

**Resumindo numa frase:** o tutorial ensina a passar de CRUD “isolado” para CRUD com relações (FK + navegação), usando EF Core Code First e scaffolding MVC.

---

## 🟦 O que é o DataContext?

O **DataContext** (em EF Core normalmente chamado **DbContext**) é a classe central que faz a ligação entre:

- o código C# (modelos)
- a base de dados relacional
- o Entity Framework Core

De forma simples:
> O DataContext é o intermediário entre a aplicação e a base de dados.

### 🧠 Analogia (muito usada em aula)

Pensa no DataContext como:
- um gestor da base de dados
- que sabe:
  - que tabelas existem
  - como elas se relacionam
  - quando deve ler, inserir, atualizar ou apagar dados

Tu falas em objetos C#, e ele traduz isso para SQL.

---

## 🟩 Relações: Foreign Key vs Navigation Property (o essencial)

### Em `Course`

```csharp
public int CategoryId { get; set; }
public Category? Category { get; set; }
```

- `CategoryId` → **Foreign Key (FK)**: coluna na BD, guarda o Id da categoria.
- `Category` → **Navigation Property**: não é coluna, é o objeto em memória para navegar (`course.Category.Name`).

### Em `Category`

```csharp
public ICollection<Course>? Courses { get; set; }
```

- Navigation property do lado “muitos”: permite navegar de uma Category para os seus Courses.

---

## 🟪 Data annotations para dinheiro

```csharp
[DataType(DataType.Currency)]
[Column(TypeName="money")]
public decimal Cost { get; set; }
```

- `[DataType(DataType.Currency)]` → influencia a forma como o campo aparece nas views (UI).
- `[Column(TypeName="money")]` → força o tipo SQL Server a ser `money`.
- `decimal` → tipo recomendado para valores monetários.

---

## 🟨 Migrations no Tutorial 4

Sempre que alteras o modelo (adicionas `Course`, adicionas relação, adicionas campos):

```powershell
Add-Migration AddCourse
Update-Database
```

- `Add-Migration` cria o “plano” (ficheiro de alterações).
- `Update-Database` aplica as alterações na BD.

---

## 🟧 Scaffolding (CRUD automático)

Ao criar `CoursesController` com “MVC Controller with views, using Entity Framework”:

- é gerado automaticamente:
  - controller
  - views CRUD (Index, Create, Edit, Details, Delete)

O scaffold escolhe automaticamente campos “legíveis” para mostrar (ex.: muitas vezes escolhe `Description`).

---

## 🟥 Seed (DbInitializer) com dados relacionais

No DbInitializer, ao criar Courses, a relação pode ser definida de duas formas:

### Forma A (FK explícita)

```csharp
CategoryId = categorias.Single(c => c.Name == "Programming").Id
```

### Forma B (Navigation Property)

```csharp
Category = categorias.Single(c => c.Name == "Administration")
```

Ambas funcionam; para consistência e clareza, é comum preferir a FK explícita no seeding.

---

## 🟥 Apagar BD no arranque (contexto do tutorial)

Para recriar dados sempre no arranque (didático):

```csharp
_context.Database.EnsureDeleted();
_context.Database.EnsureCreated();
```

---

--------------------------------------------------------------------------------------------
Homework Tutorial4
--------------------------------------------------------------------------------------------

## 🟩 HW1 — Mostrar Category.Name em vez de Description (em todas as views de Courses)

**Objetivo:** em todas as views (Index, Details, Delete, Create, Edit) onde aparece `Category.Description`, passar a mostrar `Category.Name`.

**Porque antes aparecia `Description`?**
Porque o scaffold do ASP.NET Core MVC escolhe automaticamente um campo textual “legível” para representar a Category, e encontrou `Description`.

**O que foi alterado:**
- Nas Views (Index/Details/Delete): trocar `.Description` por `.Name`.
- No Controller (Create/Edit): trocar `SelectList(..., "Description")` por `SelectList(..., "Name")`.

---

## 🟪 HW2 — Menu no Layout

Adicionar links no `Views/Shared/_Layout.cshtml` para:
- Categories (`CategoriesController`)
- Courses (`CoursesController`)

---

## 🟧 HW3 — No Create/Edit de Course, só mostrar Categories com State == true

No `CoursesController`, filtrar no `SelectList`:

```csharp
_context.Category.Where(c => c.State)
```

Aplicar em Create (GET/POST inválido) e Edit (GET/POST inválido).

---

## 🟨 HW4 — Ao listar Categories, mostrar os Courses dessa Category

### No Controller (CategoriesController)

Carregar dados relacionados:

```csharp
.Include(c => c.Courses)
```

### Na View (Categories/Index.cshtml)

Adicionar coluna “Courses” e listar os cursos:

```cshtml
<td>
    @* Verifica se a categoria tem uma coleção de cursos *@
    @* e se essa coleção não está vazia *@
    @if (item.Courses != null && item.Courses.Any())
    {
        @* <ul> cria uma lista não ordenada (unordered list) *@
        @* class="mb-0" é uma classe do Bootstrap *@
        @* mb = margin-bottom, 0 = sem margem inferior *@
        @* Serve para evitar espaço extra dentro da célula da tabela *@
        <ul class="mb-0">
            @* Percorre todos os cursos associados a esta categoria *@
            @foreach (var course in item.Courses)
            {
                @* Cada <li> é um item da lista *@
                @* Aqui mostramos o nome do curso *@
                <li>@course.Name</li>
            }
        </ul>
    }
    else
    {
        @* Caso a categoria não tenha cursos associados *@
        @* text-muted é uma classe Bootstrap para texto mais claro *@
        <span class="text-muted">No courses</span>
    }
</td>
```

---

## ✅ Ideias-chave para “bater” rápido quando fores rever

- **DbContext (DataContext)** é o intermediário entre código e BD.
- **Migrations** traduzem mudanças do modelo para SQL.
- **Foreign Key** (`CategoryId`) é coluna na BD.
- **Navigation Property** (`Category`, `Courses`) permite navegar entre objetos.
- **Include** carrega dados relacionados (senão não aparecem nas views).
- **Scaffolding** gera CRUD rápido, mas é normal ajustar depois (ex.: Name vs Description).



--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------


# Tutorial 5 – Upload e Download de Ficheiros (ASP.NET Core MVC)

## Objetivo do tutorial

Este tutorial tem como objetivo mostrar como funcionam os **uploads e downloads de ficheiros**
numa aplicação **ASP.NET Core MVC**.

O foco é compreender todo o ciclo de vida de um ficheiro numa aplicação web:

**upload → armazenamento → download**

---

## Envio de ficheiros através de formulários web

O tutorial começa por explicar como um formulário web pode enviar ficheiros
(imagens, documentos, etc.) para o servidor.

Um ponto essencial é perceber que **enviar ficheiros não é igual a enviar texto**:
- o formulário tem de estar configurado de forma específica;
- o servidor tem de saber receber e processar ficheiros corretamente.

Para que o upload funcione, o formulário deve:
- usar `method="post"`;
- incluir `enctype="multipart/form-data"`;
- utilizar inputs do tipo `file`.

Sem estas condições, os ficheiros não chegam ao servidor.

---

## Processamento dos ficheiros no servidor

Depois de os ficheiros chegarem ao servidor, o tutorial mostra como devem ser tratados.

Em vez de guardar o conteúdo dos ficheiros na base de dados, opta-se por:
- guardar os ficheiros **no sistema de ficheiros do servidor**;
- guardar na base de dados **apenas a informação necessária para os identificar**
  (por exemplo, o nome do ficheiro).

Esta abordagem mantém a base de dados mais simples e eficiente.

---

## Download de ficheiros

Na terceira parte do tutorial, o processo é invertido.

O objetivo passa a ser:
- disponibilizar os ficheiros guardados ao utilizador;
- permitir que sejam abertos ou descarregados através da aplicação.

Para isso, cria-se uma **action no controller** que:
- localiza o ficheiro no servidor;
- envia o ficheiro ao browser com o tipo correto.

---

## Porque a View passa a usar `BookViewModel`

A View **Create** passa a usar `BookViewModel` porque o formulário envia ficheiros,
e ficheiros em ASP.NET Core são recebidos como `IFormFile`.

O modelo **Book**:
- representa a entidade da base de dados;
- contém apenas tipos simples (ex.: `string`);
- é usado para persistência.

O **BookViewModel**:
- é um modelo intermédio, criado especificamente para a View;
- permite receber dados temporários do formulário;
- inclui propriedades do tipo `IFormFile`.

Desta forma:
- a View trabalha com `IFormFile`;
- o controller recebe corretamente os ficheiros;
- o modelo `Book` mantém-se limpo e focado apenas nos dados persistentes.

**Conclusão:**  
A View deve usar o modelo que melhor representa os dados que o formulário envia,
e não necessariamente a entidade da base de dados.

---

## Organização e armazenamento dos ficheiros (`wwwroot`)

Nesta fase do tutorial define-se onde os ficheiros enviados pelo utilizador vão ser
guardados fisicamente no servidor.

A pasta **`wwwroot`** é o local reservado para conteúdo acessível pela aplicação web,
como imagens, documentos e ficheiros estáticos.

São criadas duas subpastas:
- `Cover` → imagens de capa;
- `Documents` → documentos.

Esta separação mantém a aplicação organizada.

---

## Uso do `IWebHostEnvironment`

O controller não deve usar caminhos fixos (ex.: `C:\...`) porque:
- o caminho depende da máquina onde a aplicação corre;
- pode variar entre ambientes (desenvolvimento, produção).

Para resolver isto, o ASP.NET Core fornece a interface **`IWebHostEnvironment`**.

A propriedade **`WebRootPath`** devolve o caminho absoluto da pasta `wwwroot`.

Ao injetar `IWebHostEnvironment` no controller, é possível:
- construir caminhos corretos para os ficheiros;
- manter o código independente do sistema operativo e da máquina.

**Ideia-chave:**  
Os ficheiros são guardados dentro de `wwwroot`, e o acesso ao caminho físico dessa pasta
é feito através de `IWebHostEnvironment.WebRootPath`.

---

## Construção segura do caminho dos ficheiros

O ficheiro enviado pelo utilizador tem um nome (`FileName`), mas esse nome pode incluir
informação sobre o caminho no computador do utilizador.

Para evitar problemas de segurança e inconsistências, usa-se:
- `Path.GetFileName` → extrai apenas o nome do ficheiro;
- `Path.Combine` → constrói o caminho de forma segura.

O processo é:
- obter o caminho físico da pasta `wwwroot`;
- acrescentar a subpasta (`Cover` ou `Documents`);
- juntar o nome do ficheiro.

---

## Remoção de links Edit e Details

Os links **Edit** e **Details** são removidos da View **Index** para:
- simplificar a interface;
- focar o exercício exclusivamente na funcionalidade de download de ficheiros.

---

## Download: `filebytes` e `mimeType`

### O que são `filebytes`

`filebytes` é um array de bytes (`byte[]`) que contém todo o conteúdo do ficheiro
guardado no servidor.

Em termos simples:
- qualquer ficheiro (PDF, Word, imagem, etc.) é uma sequência de bytes;
- para enviar um ficheiro ao browser, o servidor tem de enviar esses bytes.

Processo:
- o ficheiro é lido do disco;
- o seu conteúdo é colocado em memória (`byte[]`);
- esses bytes são enviados ao browser.

Sem isto, o servidor não teria o conteúdo do ficheiro para devolver.

---

### O que é `mimeType`

O **MIME type** indica ao browser que tipo de ficheiro está a receber.

Exemplos:
- `application/pdf` → PDF
- `image/jpeg` → imagem JPG
- `image/png` → imagem PNG
- `application/msword` → documento Word

O browser usa esta informação para decidir:
- abrir o ficheiro diretamente;
- ou pedir ao utilizador para fazer download.

Sem o MIME type, o browser pode não saber como tratar o ficheiro.

---

## Resumo final

- `filebytes`: conteúdo do ficheiro enviado ao browser.
- `mimeType`: indica ao browser como o ficheiro deve ser tratado.
- O tutorial cobre todo o ciclo:
  
  **upload → armazenamento → download**

de forma correta numa aplicação ASP.NET Core MVC.

-------------------------------------------------------------------------------------------------
## Homework – Tutorial 5
-------------------------------------------------------------------------------------------------

### Limitação das extensões permitidas no upload

No homework é pedido que a View **Create** seja alterada de forma a mostrar apenas
extensões de ficheiros permitidas no momento da seleção.

Este comportamento é conseguido através do atributo HTML `accept` nos inputs do tipo
`file`. Este atributo indica ao browser quais os tipos de ficheiros que devem ser
apresentados no seletor de ficheiros do sistema operativo.

Desta forma, o utilizador é orientado a selecionar apenas ficheiros compatíveis
(como imagens para a capa ou documentos PDF), reduzindo erros de utilização.

É importante notar que esta limitação atua apenas ao nível da interface,
não substituindo a validação no servidor.

---

### Apresentação da imagem de capa na Index View

Por defeito, a View **Index** apresenta apenas o nome do ficheiro associado à capa
do livro.

O objetivo deste homework é substituir essa apresentação textual pela visualização
da própria imagem da capa.

Como as imagens se encontram armazenadas na pasta `wwwroot/Cover`, estas podem ser
acessíveis diretamente pelo browser. Assim, a View passa a utilizar uma tag `<img>`
que aponta para essa localização, permitindo uma apresentação visual mais intuitiva
e adequada ao contexto da aplicação.

---

### Remoção dos ficheiros ao apagar um Book

Quando um registo **Book** é removido da base de dados, os ficheiros físicos associados
(imagem de capa e documento) permanecem no servidor caso não sejam explicitamente
apagados.

Este homework pretende garantir a consistência entre a base de dados e o sistema
de ficheiros, assegurando que, ao apagar um Book, os ficheiros correspondentes são
também removidos do servidor.

Para isso, a action **Delete (POST)** do controller é alterada para:
- apagar primeiro os ficheiros existentes nas pastas `Cover` e `Documents`;
- remover apenas depois o registo da base de dados.

Esta abordagem evita a acumulação de ficheiros órfãos e contribui para uma melhor
organização e manutenção da aplicação.



--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------


# Tutorial 6 — ASP.NET Core MVC
## Endpoints, Routing, Database First, Filtros e Ordenação

---

## 1. Objetivo geral do tutorial

Este tutorial tem como objetivo desenvolver uma aplicação web em **ASP.NET Core MVC** que trabalha com **dados já existentes numa base de dados**, apresentando-os ao utilizador através de **URLs bem definidos e controlados**.

Ao longo do tutorial pretende-se demonstrar que:

- uma aplicação web **não precisa de expor a sua estrutura interna** (controllers, actions, parâmetros);
- é possível definir **URLs simples, semânticos e controlados**;
- os valores presentes nos URLs **podem e devem ser validados automaticamente**;
- o acesso aos dados pode ser **filtrado e ordenado com base no endereço do browser**.

---

## 2. Ideia central do tutorial

A ideia central do tutorial é:

> **Controlar como o utilizador acede aos dados através do URL**

Em vez de aceitar qualquer URL e qualquer valor, a aplicação:

- define regras claras sobre como os endereços devem ser escritos;
- decide que valores são válidos nesses endereços;
- encaminha apenas pedidos corretos para o código da aplicação.

Assim, o URL passa a ser parte integrante do **design da aplicação**.

---

## 3. Papel da base de dados (Database First)

Neste tutorial é usado o cenário **Database First**, o que significa que:

- a base de dados **já existe**;
- contém **tabelas reais**;
- contém **dados reais**.

A aplicação **não cria dados**, apenas os utiliza.

O tutorial mostra como:

- ligar uma aplicação ASP.NET Core MVC a uma base de dados existente;
- gerar o modelo a partir da base de dados (Scaffold);
- usar esses dados para:
  - listar,
  - filtrar,
  - ordenar informação.

---

## 4. O problema que o tutorial quer evitar (EF Core + Database First)

O Entity Framework Core **não sabe** que a base de dados já existia antes do projeto.

Para o EF:
> “Esta é a primeira vez que vejo esta base de dados.”

Se forem feitas migrations “à força”, o EF pode tentar:

- criar tabelas que já existem;
- apagar tabelas;
- alterar colunas;
- **perder dados**.

---

## 5. Porque é criada uma migration “vazia”

O tutorial manda criar uma migration, **mas não para criar a base de dados**.

O objetivo é:

> Dizer ao EF Core:  
> **“Este é o estado inicial da base de dados.”**

Isto cria um **marco zero**, a partir do qual:

- futuras alterações no código:
  - passam a ser controladas por migrations;
  - sem destruir o que já existe.

---

## 6. Porque se apaga o código em `Up()` e `Down()`

### Método `Up()`
Normalmente:
- cria tabelas;
- altera a base de dados.

Aqui isso seria perigoso porque:
- as tabelas **já existem**.

### Método `Down()`
Normalmente:
- reverte a migration;
- apaga tabelas.

Aqui isso seria ainda pior:
- poderia destruir a base de dados existente.

### Técnica usada
Criar uma **migration vazia** e guardá-la como:

> “Este é o estado inicial da BD”

Benefícios:
- o EF passa a ter histórico;
- futuras migrations funcionam corretamente;
- sem recriar nem apagar dados existentes.

---

## 7. Porque usar “MVC Controller – Empty”

Foi criado um **StudentsController (Empty)** em vez de “with views, using Entity Framework” porque:

### O que a opção automática faria
- criava CRUD completo;
- criava rotas automáticas;
- criava Views que não interessam;
- tomava decisões pelo aluno.

### Porque isso é mau neste tutorial
Este tutorial **não é sobre CRUD**, é sobre:

- routing e endpoints;
- passagem de parâmetros no URL;
- validação ao nível da rota;
- partial views;
- filtros e ordenação personalizados.

### Ao usar Empty
O aluno decide:
- que actions existem (Index, Index2, Index3);
- que parâmetros recebem;
- que queries são usadas;
- que URLs apontam para cada ação.

👉 Não há “caixa preta”.

---

## 8. Partial Views

### O que é uma Partial View

Uma partial view é:
- um **pedaço reutilizável de uma View**;
- não é uma página completa;
- não tem layout próprio;
- serve apenas para mostrar parte do HTML.

### Porque são usadas
Permitem:
- separar responsabilidades;
- reutilizar código;
- manter Views principais simples.

### No tutorial
- `Index`, `Index2`, `Index3` → definem a página
- `Listing.cshtml` → define **como a lista de estudantes é mostrada**

Isto evita duplicação de código.

---

## 9. Actions do StudentsController

### `Index`
- Lista todos os estudantes
- Usa a partial `Listing`

### `Index2(string letter)`
- Filtra estudantes pela primeira letra do nome
- Usa `async Task<IActionResult>`
- Recebe `letter` a partir do URL
- Usa `ViewBag.Letter` para formatar o menu A–Z

### Porque `async Task<IActionResult>`
- permite operações assíncronas (base de dados);
- não bloqueia threads;
- melhora escalabilidade.

---

## 10. Endpoints e routing personalizado

### O que é um endpoint
Um endpoint define:
- como um URL é interpretado;
- que parâmetros são aceites;
- para que action o pedido é encaminhado.

### Mascaramento de recursos
Por defeito, o MVC expõe:
```
/Students/Index2?letter=A
```

Com endpoints personalizados:
```
/Filter/A
```

O URL passa a representar um **recurso lógico**, não a implementação interna.

---

## 11. Endpoint de filtragem (`Filter/{letter}`)

```csharp
app.MapControllerRoute(
    name: "filtered",
    pattern: "Filter/{letter?}",
    defaults: new { Controller = "Students", Action = "Index2" },
    constraints: new { letter = @"[A-ZÂÁÉÓ]" }
);
```

**Características:**

- `letter` é opcional;
- só aceita letras válidas;
- validação ocorre antes da action;
- pedidos inválidos não chegam ao código.

---

## 12. Porque as variantes não podem coexistir

O guião diz:
> *these variants must not be defined simultaneously because they overlap in the pattern*

**Razão:**
- todas usam o mesmo padrão base (`Filter/{letter}`);
- o ASP.NET Core avalia rotas por ordem;
- rotas concorrentes causam ambiguidades.

👉 É uma limitação natural do routing.

---

## 13. Menu A–Z (Index2)

- construído dinamicamente com um ciclo `for`;
- **letra ativa:**
  - aparece a vermelho e a negrito;
  - não é link;
- **restantes letras:**
  - são links;
  - usam `asp-route="filtered"`.

---

## 14. Index3 — Ordenação dos dados

### Objetivo
Apresentar a lista de estudantes ordenada por:
- número ou nome;
- ordem ascendente ou descendente.

### URLs pretendidos
```bash
/order/byNumber/Ascending
/order/byNumber/Descending
/order/byName/Ascending
/order/byName/Descending
```

---

## 15. Endpoint de ordenação

```csharp
app.MapControllerRoute(
    name: "ordered",
    pattern: "order/{orderBy}/{direction}",
    defaults: new { Controller = "Students", Action = "Index3" }
);
```

Este endpoint:
- mascara controller e action;
- passa parâmetros semânticos para a action;
- permite URLs claros e legíveis.

---

## 16. Index3 — lógica no Controller

- recebe `orderBy` e `direction`;
- decide o campo e o sentido da ordenação;
- usa `ViewBag.OrderBy` e `ViewBag.Direction`;
- envia os dados ordenados para a View.

---

## 17. Menu de ordenação (Index3)

A View apresenta 4 links:
1. ByNumber
2. ByName
3. Ascending
4. Descending

**Regras:**
- opção ativa → vermelho + negrito;
- opção inativa → link;
- mudar um parâmetro preserva o outro.

A lógica é idêntica ao menu de letras.

---

## 18. O que se aprende no final do tutorial

No final deste tutorial percebe-se que:

- URLs fazem parte do design da aplicação;
- um URL pode representar uma ação clara sobre os dados;
- endpoints permitem:
  - mascarar implementação interna;
  - validar pedidos antes da action;
- filtros e ordenações podem ser controlados via URL;
- partial views promovem reutilização e clareza;
- Database First pode ser usado de forma segura com EF Core.


--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------


# Tutorial 7 — Cookies, Sessões e Login (ASP.NET Core MVC)

Este tutorial tem como objetivo perceber **como uma aplicação Web mantém “estado”** (informação entre pedidos) apesar de o **HTTP ser stateless**.

- **HTTP stateless**: cada request é independente; o servidor não “se lembra” do anterior.
- Para manter estado usamos **cookies (cliente)** e **sessões (servidor)**.

---

## 1. Ferramentas para observar o que acontece (DevTools)

Para ver cookies e redirects, usa as **DevTools** do browser:

- **Edge/Chrome**: `F12` (ou `Ctrl+Shift+I`)
- **Opera GX**: `Ctrl+Shift+I`

### Onde ver o quê
- **Network → Headers**
  - **Response Headers**: `Set-Cookie` (cookies a serem criados/apagados)
  - **Request Headers**: `Cookie` (cookies que o browser enviou)
- **Application → Cookies**
  - lista de cookies guardados e coluna **Expires** (`Session` ou data)

> Nota: o Network só mostra pedidos feitos **depois** de abrires o separador (faz `F5` com o Network aberto).

---

## 2. Parte 1 — Testar Web Cookies (cliente)

### 2.1 O que é um cookie (resumo)
Cookie = **par nome=valor** guardado no browser e enviado automaticamente ao servidor nos pedidos seguintes.

- **Servidor cria** cookies via `Set-Cookie` (response)
- **Browser guarda** e **reenvi**a via `Cookie` (request)

### 2.2 Tipos de cookies
**Cookie de sessão**
- Sem `Expires/Max-Age`
- aparece como `Session` na coluna Expires
- dura “enquanto a sessão do browser existir”

**Cookie persistente**
- Tem `Expires` ou `Max-Age`
- dura além de fechar o browser (em modo normal)
- em janela anónima, normalmente **tudo é limpo ao fechar** a janela

### 2.3 Criar cookies (exemplo do tutorial)

#### Controller (AddCookies)
- cria 3 cookies:
  - `Test1`: session cookie
  - `Test2`: persistente (10s)
  - `Teste3`: persistente (1 dia)

**O que observar**
- `Network → AddCookies → Response Headers`:
  - `Set-Cookie: Test1=...`
  - `Set-Cookie: Test2=...; expires=...`
  - `Set-Cookie: Teste3=...; expires=...`
- Pedidos seguintes:
  - `Network → (/) ou Index → Request Headers`:
    - `Cookie: Test1=...; Test2=...; Teste3=...`

### 2.4 Mostrar cookies recebidos no request (View)
A view percorre `Context.Request.Cookies` e imprime:
- nome do cookie
- valor do cookie

**Ideia-chave**
- a view mostra cookies **que o browser enviou** naquele request.

### 2.5 Apagar cookies (DeleteCookies)
Apagar cookies é feito enviando `Set-Cookie` com data expirada (ex.: 1970).
**O que ver**
- `Network → DeleteCookies → Response Headers` com `Set-Cookie` a expirar cookies.

---

## 3. Parte 2 — Session Variables (servidor)

### 3.1 O que são variáveis de sessão
Variáveis de sessão = dados guardados **no servidor**, associados a um cliente, reutilizáveis entre requests.

Exemplos:
- `username` autenticado
- `id` carrinho de compras
- permissões/role

**Mas**: o servidor precisa de saber *qual é a sessão* → usa-se um **cookie de sessão** com um **Session ID**.

### 3.2 Como a sessão funciona (mecanismo)
1. Servidor cria um **Session ID**
2. Envia ao browser num cookie (ex.: `.AspNetCore.Session` ou `.Class07b.Session`)
3. Browser guarda e reenvi**a** em cada pedido
4. Servidor usa o Session ID para recuperar os dados (memória/cache)

---

## 4. Configuração de Sessões no `Program.cs`

### 4.1 Registar serviços (AddSession)
Exemplo usado no tutorial:

```csharp
builder.Services.AddSession(option =>
{
    option.IdleTimeout = TimeSpan.FromMinutes(10);
    option.Cookie.Name = ".Class07b.Session";
});
```

- `IdleTimeout`: expira sessão após 10 min sem atividade
- `Cookie.Name`: nome do cookie que identifica a sessão (Session ID)

> Normalmente também é necessário `AddDistributedMemoryCache()` quando a sessão é em memória.

### 4.2 Ativar middleware (UseSession)

```csharp
app.UseSession();
```

- ativa o middleware que:
  - lê cookie de sessão do request
  - carrega a sessão para `HttpContext.Session`
  - cria uma nova sessão quando necessário

---

## 5. Criar e visualizar session variables

### 5.1 Adicionar variáveis de sessão (Controller)
Exemplo:

- `SetString("StringValue", "...")`
- `SetInt32("IntegerValue", 100)`

**O que observar**
- `Set-Cookie: .Class07b.Session=...` (quando a sessão é criada)
- `Cookie: .Class07b.Session=...` (em pedidos seguintes)

### 5.2 Mostrar na View (Session ID + Keys)
A view mostra:
- `@Context.Session.Id` (para confirmar reutilização)
- percorre `Context.Session.Keys` e usa:
  - `GetString` para `"StringValue"`
  - `GetInt32` para as restantes

**Ideia-chave**
- Se o Session ID se mantiver, a sessão está a ser reutilizada.

---

## 6. Apagar dados de sessão vs terminar sessão

### 6.1 Apagar apenas variáveis (mantém sessão)
- remove as chaves com `Session.Remove(key)`
- sessão continua (cookie mantém-se)

### 6.2 Terminar sessão (logout “real” do tutorial)
- apaga o cookie da sessão (ex.: `.Class07b.Session`)
- “elimina todas as session variables” (efeito prático do tutorial)
- no próximo pedido, será criada uma sessão nova e vazia

---

## 7. Parte 3 — Aplicação real: Login + Logout + Preferências

### 7.1 Objetivo
Aplicar cookies + sessão num cenário real:
- autenticação de utilizador
- funcionalidades só disponíveis se o utilizador estiver identificado (sessão ativa)

---

## 8. Base de Dados + EF Core (para o Login)

### 8.1 Modelo `User` (como no teu projeto)
```csharp
public class User
{
    [Key]
    [Required]
    public string Username { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; }
}
```

- `Username` é **Primary Key** (não há Id)
- `DataType.Password` afeta o input na view (type=password)

### 8.2 Gerar DbContext + CRUD (TIP do enunciado)
- Add → **MVC Controller with views, using Entity Framework**
- escolhe Model: `User`
- cria o `DbContext`, controller e views

### 8.3 Migrations
- `Add-Migration InitialCreate`
- `Update-Database`

### 8.4 Inserir utilizador de teste (SQL Server Object Explorer)
- abrir `dbo.Users` → **View Data**
- inserir:
  - `Username = xpto`
  - `Password = 1234`

---

## 9. Login

### 9.1 View Login
Criar View `Login.cshtml` (pode ser template Create do model User) com:
- username
- password
- botão Login

### 9.2 Controller Login (GET + POST)
- GET devolve o formulário
- POST valida credenciais na BD
- se correto:
  - cria variável de sessão `"user"` com o username
  - redireciona para Home

**Após login com sucesso**
- cookie de sessão existe
- `Session["user"]` existe
- menus/áreas restritas ficam disponíveis

---

## 10. Logout

- Logout termina sessão apagando o cookie da sessão:

```csharp
HttpContext.Response.Cookies.Delete(".Class07b.Session");
```

**Como interpretar no Network**
- No **Request** para `/Users/Logout`, o browser ainda envia o cookie (porque ainda não foi apagado).
- O apagamento acontece na **Response** (Set-Cookie expirado).

---

## 11. Preferências (light/dark mode) com cookie persistente

### 11.1 Controller Preferences
- GET: lê cookie `viewMode` (default `light`)
- POST: grava cookie `viewMode` com `Expires` (ex.: 1 ano)

### 11.2 View Preferences (EMPTY)
Criar view **Empty** e colar o form com radio buttons:
- light
- dark
- botão Go

### 11.3 Aplicar CSS no `_Layout.cshtml`
O layout escolhe stylesheet com base no cookie:

- se `viewMode == "dark"` → `site_dark.css`
- senão → `site.css`

> `asp-append-version="true"` ajuda a evitar cache desatualizada dos ficheiros estáticos.

---

## 12. Homework (extensão)
### 12.1 Register (novo utilizador)
- criar action `Register` (GET + POST)
- garantir que não existe outro user com o mesmo `Username`
- após registo, fazer **login automático** (criar sessão `"user"`)

### 12.2 Menu
- adicionar opção **Register** no menu
- só deve aparecer quando **não está logado** (sem `"user"` na sessão)

---

## 13. Checklist de testes (rápido)
1. AddCookies → ver `Set-Cookie` no response
2. Index → ver `Cookie:` no request
3. Session variables → ver `Session.Id` manter-se
4. DeleteSessionVariables → limpa valores mas mantém sessão
5. DeleteSession/Logout → termina sessão (cookie apagado na response)
6. Login com `xpto/1234` → cria sessão `"user"`
7. Preferences dark → grava cookie `viewMode=dark` (persistente)
8. Logout → menus voltam ao estado “não autenticado”



--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------



# Tutorial 8 — ASP.NET Core MVC Security (Identity, Authentication, Authorization e Roles)

## Objetivo do Tutorial

Este tutorial tem como objetivo desenvolver uma aplicação **ASP.NET Core MVC**
para testar e compreender os mecanismos de **segurança** disponibilizados pelo framework.

No final do tutorial, é possível:
- registar utilizadores;
- efetuar login e logout;
- proteger páginas para apenas utilizadores autenticados;
- controlar permissões através de **roles** (ex.: Admin e Client).

O ASP.NET Core disponibiliza um sistema de autenticação integrado (**Identity**),
mas permite também a utilização de serviços externos (Facebook, Twitter, LinkedIn)
e a gestão segura de segredos da aplicação (connection strings, chaves de API, etc.).

---

## Authentication vs Authorization

### Authentication (Autenticação) — “Quem és tu?”

A autenticação é o processo de **provar a identidade** de um utilizador.

Funcionamento:
- o utilizador fornece credenciais (username/email + password);
- o sistema compara com os dados guardados na base de dados do Identity;
- se coincidirem, o utilizador fica autenticado.

Exemplo:
> “Sou o Diogo, aqui está a minha palavra-passe.”  
> O sistema valida e confirma a identidade.

Resultado prático:
- é criada uma sessão de login (normalmente através de cookies).

---

### Authorization (Autorização) — “O que podes fazer?”

Após a autenticação, o sistema decide **o que o utilizador pode ou não fazer**.

Aqui entram:
- permissões;
- roles (Admin, Client);
- regras de acesso a páginas e ações.

Exemplo:
- Utilizador autenticado:
  - pode ver o seu perfil;
  - não pode aceder ao painel de administração se não for Admin.

---

## ASP.NET Core Identity (visão geral)

Neste tutorial são utilizados:
- **Identity** — sistema de autenticação do ASP.NET Core;
- `[Authorize]` — restringe acesso a utilizadores autenticados;
- `[AllowAnonymous]` — permite acesso público a ações específicas;
- **Roles** — controlo de permissões (Admin, Client).

---

## Criação do Projeto com Identity

O projeto deve ser criado como:
- **ASP.NET Core Web App (MVC)**
- Autenticação: **Individual Accounts**

O template cria automaticamente:
- `appsettings.json` com connection string;
- migrations do Identity;
- `ApplicationDbContext`;
- configuração do Identity no `Program.cs`.

Para criar a base de dados:
```powershell
Update-Database
```

---

## Alterar as Views de Autenticação (Scaffolding)

Por defeito, as páginas de **Login** e **Register** usam código interno do Identity,
que não é editável diretamente.

Para poder modificar estas páginas, é necessário fazer **Scaffolding do Identity**:

1. Botão direito no projeto  
2. **Add → New Scaffold Item…**  
3. Selecionar **Identity**  
4. Escolher as páginas **Login** e **Register**

Após o scaffolding:
- as páginas ficam em `Areas/Identity/Pages/Account`;
- cada funcionalidade tem:
  - `.cshtml` (View);
  - `.cshtml.cs` (Code-behind).

Este passo é essencial para personalizar o processo de autenticação.

---

## Alterar o Registo para usar Username

### InputModel (Register.cshtml.cs)

Adicionar o campo `UserName`:

```csharp
[Required]
[Display(Name = "User Name")]
public string UserName { get; set; }
```

Na criação do utilizador:

```csharp
await _userStore.SetUserNameAsync(user, Input.UserName, CancellationToken.None);
await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
```

### View (Register.cshtml)

```html
<input asp-for="Input.UserName" class="form-control" />
```

---

## Criar Perfil de Utilizador (Informação Extra)

Para guardar dados adicionais, cria-se uma entidade separada do Identity.

### Modelo Perfil

```csharp
public class Perfil
{
    public int Id { get; set; }

    [DataType(DataType.Date)]
    public DateTime Birthday { get; set; }

    public string? UserName { get; set; }
}
```

### DbContext

```csharp
public DbSet<Perfil> Perfils { get; set; }
```

Após alterações:

```powershell
Add-Migration AddPerfil
Update-Database
```

---

## Adicionar Birthday ao Registo

### InputModel

```csharp
[Required]
[DataType(DataType.Date)]
public DateTime Birthday { get; set; }
```

### View

```html
<input asp-for="Input.Birthday" type="date" class="form-control" />
```

### Guardar Perfil

```csharp
Perfil newPerfil = new Perfil
{
    UserName = user.UserName,
    Birthday = Input.Birthday
};

_dbcontext.Perfils.Add(newPerfil);
_dbcontext.SaveChanges();
```

---

## Alterar o Login para usar Username

### InputModel (Login)

```csharp
[Required]
public string UserName { get; set; }
```

### Login

```csharp
await _signInManager.PasswordSignInAsync(
    Input.UserName,
    Input.Password,
    Input.RememberMe,
    lockoutOnFailure: false);
```

---

## _Layout.cshtml e _LoginPartial.cshtml

- `_Layout.cshtml` define a estrutura geral da aplicação;
- `_LoginPartial.cshtml` mostra:
  - **Login / Register** se o utilizador não estiver autenticado;
  - **Hello <username> / Logout** se estiver autenticado.

---

## Proteger Páginas com [Authorize]

```csharp
[Authorize]
public class HomeController : Controller
{
}
```

Apenas utilizadores autenticados conseguem aceder.

Para permitir acesso público:

```csharp
[AllowAnonymous]
```

---

## Authorization com Roles

### Ativar Roles no Program.cs

```csharp
builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
        options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
```

---

## Criar Roles (Seed)

```csharp
public static class SeedRoles
{
    public static async Task SeedAsync(RoleManager<IdentityRole> roleManager)
    {
        if (!await roleManager.Roles.AnyAsync())
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
            await roleManager.CreateAsync(new IdentityRole("Client"));
        }
    }
}
```

### Chamada no Program.cs

```csharp
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await SeedRoles.SeedAsync(roleManager);
}
```

---

## Atribuir Role no Registo

```csharp
await _userManager.AddToRoleAsync(user, Input.Role);
```

---

## Proteger Actions por Role

```csharp
[Authorize(Roles = "Admin")]
public IActionResult OnlyAdmins() { }

[Authorize(Roles = "Client")]
public IActionResult OnlyClients() { }

[Authorize(Roles = "Admin,Client")]
public IActionResult OnlyClientsAndAdmins() { }
```

---

## Mostrar Links no Menu conforme Role

No `_Layout.cshtml`:

```cshtml
@if (User.IsInRole("Client")) { ... }
@if (User.IsInRole("Admin")) { ... }
```

---

## Conclusão

Este tutorial demonstrou como:
- implementar autenticação com ASP.NET Core Identity;
- personalizar registo e login;
- guardar dados adicionais do utilizador;
- proteger páginas e ações;
- aplicar autorização baseada em roles.

Este modelo constitui a base de segurança de aplicações **ASP.NET Core MVC**.



--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------


# Tutorial 9 — Cache HTTP e Action Filters Personalizados

**Unidade:** Web Engineering (2024/2025)
**Tecnologia:** ASP.NET Core MVC

Este tutorial está dividido em **duas partes**:

* **Parte A — Cache HTTP**: compreender e testar cache de respostas HTTP (browser, proxies/CDN), headers e `[ResponseCache]`.
* **Parte B — Action Filter personalizado**: criar um filtro de manutenção diária que restringe o acesso por intervalo horário.

O objetivo é **entender o porquê**, **ver o que acontece nos headers** e **aplicar passo a passo**.

---

## Parte A — Cache HTTP

### 1. O que é cache (ideia simples)

**Cache** é guardar uma resposta (em memória ou disco) para **não a gerar novamente** durante um certo período.

* Sem cache: cada pedido executa código, acede à BD e gera HTML.
* Com cache: a resposta é **reutilizada** por *X segundos*.

**Resultado:** melhor desempenho e menor carga no servidor.

---

### 2. Cache na Web (o que acontece de verdade)

#### Sem cache

1. Browser faz o pedido.
2. Servidor executa C#, vai à BD e gera HTML.
3. Envia a resposta.
4. Repete tudo no próximo pedido.

➡️ Lento e pesado.

#### Com cache

1. Browser pede a página.
2. Servidor indica que a resposta pode ser guardada por *X segundos*.
3. Browser guarda.
4. Durante *X segundos*, **não volta a pedir** ao servidor.

➡️ Rápido e eficiente.

---

### 3. Onde a cache pode existir

1. **Cache no cliente (browser)**

   * Guardada localmente.
   * Só o próprio utilizador usa.
   * Ex.: HTML, CSS, JS.

2. **Cache intermédia (proxy / CDN)**

   * Entre o browser e o servidor.
   * Pode servir **vários utilizadores**.
   * Só usada se a resposta for marcada como **public**.

> ⚠️ Cache **não é base de dados**: é temporária, pode desaparecer e não é a fonte oficial dos dados.

---

### 4. Headers HTTP (quem manda no cache)

O header mais importante é **`Cache-Control`**.

Exemplos:

* **Sem cache**

  ```http
  Cache-Control: no-store
  ```

* **Cache privada (só browser)**

  ```http
  Cache-Control: private, max-age=20
  ```

* **Cache pública (browser + proxies/CDN)**

  ```http
  Cache-Control: public, max-age=40
  ```

Outras diretivas úteis:

* `no-store` → não guardar nada
* `no-cache` → pode guardar, mas tem de revalidar
* `max-age=N` → tempo (em segundos) em que a resposta é considerada “fresca”

> Cache **não decide autenticação/autorização**; decide apenas **reutilização da resposta**.

---

### 5. Provar que está em cache (DevTools)

No **DevTools → Network** podes observar:

* respostas a não mudarem (ex.: hora na View);
* indicações como **(from memory cache)** / **(from disk cache)**;
* headers que confirmam o comportamento.

> O tutorial pede colocar a **hora na View** para verificar se muda ou não.

---

### 6. `Vary`: cache diferente por header

Se a cache depender do **User-Agent**, o servidor envia:

```http
Vary: User-Agent
```

Significa:

* caches diferentes para browsers diferentes;
* Chrome ≠ Firefox ≠ Mobile.

---

### 7. Revalidação: `Last-Modified`, `If-Modified-Since` e `304`

Para **ficheiros estáticos** (CSS, JS, imagens):

* Servidor envia:

  ```http
  Last-Modified: Tue, 10 Dec 2024 14:30:00 GMT
  ```
* Quando a cache expira, o browser pergunta:

  ```http
  If-Modified-Since: Tue, 10 Dec 2024 14:30:00 GMT
  ```
* Se não mudou:

  ```http
  304 Not Modified
  ```

➡️ O ficheiro **não é reenviado** (muito eficiente).

---

## Parte A — Implementação passo a passo

### 8. Cache Profiles (Program.cs)

**O que são?**

* Conjunto de regras de cache com um **nome**;
* Configuradas **uma única vez**;
* Reutilizáveis em várias actions/controllers.

```csharp
builder.Services.AddControllersWithViews(options =>
{
    options.CacheProfiles.Add(
        "TestCacheProfile",
        new Microsoft.AspNetCore.Mvc.CacheProfile
        {
            Duration = 20,
            Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.Any,
            VaryByHeader = "User-Agent"
        });
});
```

Uso numa action:

```csharp
[ResponseCache(CacheProfileName = "TestCacheProfile")]
public IActionResult WithHeaders()
{
    return View();
}
```

---

### 9. Response Caching (middleware)

Registo:

```csharp
builder.Services.AddResponseCaching(options =>
{
    options.SizeLimit = 200 * 1024 * 1024;      // 200 MB
    options.MaximumBodySize = 20 * 1024 * 1024; // 20 MB
});
```

Pipeline:

```csharp
app.UseResponseCaching();
```

---

### 10. Cache de ficheiros estáticos (`UseStaticFiles`)

```csharp
using Microsoft.Net.Http.Headers;

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers[HeaderNames.CacheControl] =
            "public, max-age=20";

        ctx.Context.Response.Headers[HeaderNames.LastModified] =
            ctx.File.LastModified.ToUniversalTime().ToString("R");
    }
});
```

> ⚠️ Testar **ficheiros reais em `wwwroot`** (ex.: `/css/site.css`).

---

## Parte B — Action Filters Personalizados

### 11. O que é um Action Filter

Código que corre **à volta** da action.

Principais *hooks*:

* `OnActionExecuting()` → antes da action
* `OnActionExecuted()` → depois da action
* `OnResultExecuting()` → antes do resultado (View)
* `OnResultExecuted()` → depois do resultado

---

### 12. Objetivo no tutorial

Criar um filtro de **manutenção diária**:

* verifica a hora atual;
* compara com `From` e `To`;
* se estiver no intervalo, **bloqueia** a action;
* redireciona para `Security/Maintenance`.

Vantagem: regra **transversal**, sem `if` repetidos.

---

### 13. Filtro parametrizado (`DailyMaintenanceFilter`)

**Estrutura:** `Filters/DailyMaintenanceFilter.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Class09.Filters
{
    public class DailyMaintenanceFilter : ActionFilterAttribute
    {
        // [horas, minutos, segundos]
        public int[] From { get; set; }
        public int[] To { get; set; }

        private TimeSpan _from, _to;

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            _from = new TimeSpan(From[0], From[1], From[2]);
            _to   = new TimeSpan(To[0],   To[1],   To[2]);

            DateTime nowDt = DateTime.Now;
            TimeSpan now = new TimeSpan(nowDt.Hour, nowDt.Minute, nowDt.Second);

            bool inMaintenance =
                (_from <= _to && now >= _from && now <= _to) ||
                (_from >  _to && (now >= _from || now < _to));

            if (inMaintenance)
            {
                context.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(
                        new { controller = "Security", action = "Maintenance" }
                    ));
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
```

---

### 14. Aplicar o filtro

```csharp
[DailyMaintenanceFilter(From = new[] { 22, 30, 0 }, To = new[] { 23, 59, 59 })]
public IActionResult Index()
{
    return View();
}
```

Atravessando a meia-noite:

```csharp
[DailyMaintenanceFilter(From = new[] { 23, 0, 0 }, To = new[] { 2, 0, 0 })]
```

---

### 15. Página de manutenção

```csharp
public class SecurityController : Controller
{
    public IActionResult Maintenance()
    {
        return View();
    }
}
```

--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------

  
# Tutorial 10 — AJAX com jQuery Unobtrusive AJAX em ASP.NET Core MVC

## Objetivo do Tutorial

O objetivo do **Tutorial 10** é aprender a tornar uma aplicação **ASP.NET Core MVC**
mais **fluida e interativa**, evitando o recarregamento completo da página.

Em vez de um *submit* tradicional (que faz navegação e refresh), utiliza‑se **AJAX**
para:
- enviar pedidos HTTP em *background*;
- receber respostas do servidor;
- atualizar **apenas partes específicas do HTML** (DOM).

O resultado é uma aplicação que **parece uma Single Page Application**, mas continua
a usar o **MVC clássico**.

---

## 1. O que é AJAX (e o que acontece “por trás”)

**AJAX** significa *Asynchronous JavaScript and XML*.  
Hoje em dia, na prática, raramente se usa XML — o mais comum é **HTML parcial** ou **JSON**.

### Fluxo real de um pedido AJAX

1. O utilizador clica num link ou submete um formulário.
2. O browser **não navega** para outra página.
3. JavaScript envia um pedido HTTP (**GET** ou **POST**) em *background*.
4. O servidor responde (texto, HTML parcial, JSON, etc.).
5. JavaScript insere a resposta no DOM, num elemento específico.

**Resultado:**  
A página não recarrega → só uma parte muda → melhor experiência de utilizador.

---

## 2. O que significa “Unobtrusive AJAX”

*Unobtrusive* significa **não misturar JavaScript diretamente no HTML** (ex.: `onclick="..."`).

Em vez disso:
- o comportamento AJAX é configurado com **atributos HTML**;
- um **plugin JavaScript** lê esses atributos e transforma automaticamente o link/form
  num pedido AJAX.

Exemplo:

```html
<form data-ajax="true" data-ajax-method="post" data-ajax-update="#Tabela">
```

### Vantagens
- HTML mais limpo;
- comportamento declarativo;
- menos JavaScript manual;
- separação clara entre estrutura e comportamento.

---

## 3. Principais atributos `data-ajax-*`

Os mais importantes usados no tutorial:

### `data-ajax="true"`
Ativa o comportamento AJAX no link ou formulário.

### `data-ajax-method="get | post"`
Define o método HTTP do pedido.

> ⚠️ A action no controller **tem de aceitar esse método**.

### `data-ajax-update="#alvo"`
Indica **onde a resposta do servidor será inserida** no DOM.

### `data-ajax-mode="replace | before | after"`
Define como a resposta é inserida:
- `replace` → substitui o conteúdo do elemento alvo;
- `before` → insere antes;
- `after` → insere depois.

### `data-ajax-loading="#spinner"`
Mostra/esconde um elemento (ex.: GIF) enquanto o pedido está a decorrer.

### `data-ajax-success="funcaoJS"`
Chama uma função JavaScript quando o pedido termina com sucesso.

---

## 4. AJAX em MVC: Actions e Partial Views

Quando se usa AJAX, as **actions do controller** não devem devolver páginas completas.

Existem duas abordagens comuns:

### A) Devolver texto simples
Usado para testes ou respostas muito pequenas.

```csharp
return "OK";
```

### B) Devolver HTML parcial (Partial View)
Abordagem típica em CRUD.

```csharp
return PartialView("Listing", model);
```

No browser, esse HTML é inserido **apenas no alvo definido** (`#Tabela`, por exemplo).

---

## 5. Porque AJAX é ideal para CRUD

### CRUD sem AJAX
- Cada Create/Edit/Delete recarrega a página toda;
- UX mais lenta e pesada.

### CRUD com AJAX
- Só a parte necessária é atualizada;
- Melhor experiência e menos distrações.

No tutorial:

- **Create** → atualiza a **tabela inteira**
- **Edit inline** → atualiza **apenas uma célula**
- **Delete** → remove e atualiza sem refresh

---

## 6. Onde a resposta do servidor entra

O padrão é sempre o mesmo:

1. Existe um elemento HTML com `id`:
   ```html
   <table id="Tabela"></table>
   ```
2. O AJAX aponta para ele:
   ```html
   data-ajax-update="#Tabela"
   ```

### Edit inline (caso especial)

Cada linha/célula tem um id próprio:

```html
<td id="Col_5">Maria</td>
```

E o AJAX usa:

```html
data-ajax-update="#Col_5"
```

Isto permite atualizar **exatamente aquela célula**.

---

## 7. Passo a passo — Implementação do Tutorial

### 7.1 Criar o projeto
- ASP.NET Core Web App (MVC)
- Instalar o pacote:
```powershell
Install-Package Microsoft.jQuery.Unobtrusive.Ajax
```

---

### 7.2 Formulário Create com AJAX (Index.cshtml)

```html
<form asp-action="Create"
      data-ajax="true"
      data-ajax-method="post"
      data-ajax-update="#Tabela"
      data-ajax-mode="replace"
      data-ajax-success="sucesso">

    <input id="inp" type="text" name="NewName" />
    <input type="submit" value="Create" />
</form>
```

---

### 7.3 Action Create (POST)

```csharp
[HttpPost]
public IActionResult Create(string NewName)
{
    var p = new Person { Name = NewName };
    _context.Person.Add(p);
    _context.SaveChanges();

    return PartialView("Listing", _context.Person);
}
```

---

### 7.4 Partial View Listing

```cshtml
<td id="Col_@item.Id">
    @item.Name
</td>
```

---

### 7.5 Edit inline — GET

```csharp
public IActionResult Edit(int id)
{
    var p = _context.Person.SingleOrDefault(x => x.Id == id);
    return PartialView("Edit", p);
}
```

---

### 7.6 Edit inline — POST

```csharp
[HttpPost]
public string Edit(Person p)
{
    _context.Update(p);
    _context.SaveChanges();
    return p.Name;
}
```

---

## 8. Assíncrono e Loading Spinner

Como o pedido é assíncrono:
- a página não bloqueia;
- o utilizador pode continuar a interagir;
- pode-se mostrar um loader para feedback visual.

Isto contribui para a sensação de rapidez.

---

## 9. Extra — Limpar a textbox após Create

Como não há refresh, o input mantém o valor.  
Resolve‑se com JavaScript:

```html
<script>
    function sucesso() {
        $("#inp").val("");
    }
</script>
```

Ligado ao formulário com:
```html
data-ajax-success="sucesso"
```

---

## 10. Regra final (muito importante)

> **`data-ajax-method` define o método HTTP do pedido.**  
> **A action do controller tem de aceitar esse método.**

AJAX **não muda o MVC** — apenas muda **como a resposta é usada no browser**.


--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------
--------------------------------------------------------------------------------------------------------------------------------------------------------



# Tutorial 11 — jQuery, jQuery UI e AJAX

## Objetivo do Tutorial

Este tutorial tem como objetivo introduzir e consolidar a utilização de **jQuery** e **jQuery UI** numa aplicação **ASP.NET Core MVC**, demonstrando como melhorar a interatividade da interface, reduzir a carga no servidor e separar claramente a lógica de negócio da lógica de apresentação.

Ao longo do tutorial, são explorados eventos, manipulação do DOM, temporização, comunicação assíncrona (AJAX) e integração de bibliotecas externas.

---

## 1. Motivação: Porquê jQuery?

### Ideia‑chave

* Separar **INTERAÇÃO** da **LÓGICA**
* Tornar a aplicação mais **rápida e fluida**
* Evitar **pedidos desnecessários** ao servidor

### Como seria sem jQuery (abordagem clássica)

Sem JavaScript/jQuery, ações simples como mudar texto ou cor implicariam:

1. Clique do utilizador
2. Envio de um POST para o controller
3. Execução no servidor
4. Devolução de uma nova View
5. Recarregamento da página

**Problemas desta abordagem**:

* Lentidão
* Tráfego HTTP excessivo
* Código repetitivo nos controllers
* Má experiência de utilização (piscadelas da página)

---

## 2. Vantagens do jQuery

### 2.1 Rapidez (performance percebida)

Com jQuery, muitas ações são executadas **no browser**, de forma imediata, sem depender do servidor.

### 2.2 Melhor experiência do utilizador (UX)

* Feedback imediato
* Animações suaves
* Interações naturais

> UX não é apenas estética — é resposta rápida.

### 2.3 Menos carga no servidor

* Sem jQuery: cada interação gera um request
* Com jQuery: só se comunica com o servidor quando necessário

Resultado:

* Aplicações mais escaláveis
* Servidores mais simples

### 2.4 Controllers mais limpos

Os controllers deixam de tratar:

* Lógica visual
* Decisões de interface

Passam a focar‑se em:

* Regras de negócio
* Acesso a dados
* Validações importantes

---

## 3. A grande vantagem escondida: Separação de responsabilidades

| Camada        | Responsabilidade          |
| ------------- | ------------------------- |
| Controller    | Dados e regras            |
| View + jQuery | Comportamento e interação |

Esta separação prepara o aluno para:

* APIs REST
* Single Page Applications
* Frameworks modernos (React, Angular, Vue)

---

## 4. Porque é que jQuery aparece na cadeira

Embora seja uma tecnologia mais antiga, o jQuery ensina conceitos fundamentais:

* Eventos
* Manipulação do DOM
* Estado no cliente
* AJAX
* Programação assíncrona
* Dependências entre bibliotecas

> Quem entende jQuery entende qualquer framework frontend.

---

## 5. `id` vs `class`

### 5.1 `id`

* Identificador **único** na página
* Não pode repetir

```html
<p id="box">Texto</p>
```

Seleção:

```js
$('#box')
```

Usar quando:

* O elemento é único
* Existe comportamento específico

---

### 5.2 `class`

* Identificador reutilizável
* Pode existir em vários elementos

```html
<p class="square">Texto 1</p>
<p class="square">Texto 2</p>
```

Seleção:

```js
$('.square')
```

Usar quando:

* Elementos partilham estilos
* Elementos partilham comportamentos

---

### 5.3 Regra de ouro

* Se só existir um → `id`
* Se puderem existir vários → `class`

---

## 6. JSON e `JsonConvert`

### Problema comum

`JsonConvert` não existe por defeito no ASP.NET Core.

### Causa

Pertence ao pacote **Newtonsoft.Json**.

### Solução (obrigatório)

Instalar o pacote:

* `Newtonsoft.Json`
* Adicionar:

```csharp
using Newtonsoft.Json;
```

> ⚠️ Este passo é essencial e não deve ser esquecido.

---

## 7. Introdução ao jQuery UI

### O que é o jQuery UI

Framework de interface gráfica construído sobre jQuery.

Fornece **widgets prontos**, como:

* Tabs
* Autocomplete
* Dialog
* Datepicker
* Slider

Inclui ainda:

* Interactions
* Effects
* Utilities

---

## 8. Integração do jQuery UI no projeto (LibMan + wwwroot)

Nesta parte do tutorial, o objetivo é integrar um **framework externo** (jQuery UI) no projeto ASP.NET Core MVC.

### 8.1 Instalação com LibMan (Client-Side Library)

1. No *Solution Explorer*, clicar com o botão direito no projeto (ou em `wwwroot`) → **Add** → **Client-Side Library**.
2. **Provider**: selecionar `cdnjs` (ou outro provider sugerido pelo guião).
3. No campo **Library**:

   * escrever `jqueryui` e selecionar uma opção válida através do IntelliSense (a versão pode variar).
4. Selecionar **Include all library files**.
5. **Target Location** recomendado:

   * `wwwroot/lib/jqueryui/`

> Nota: caso o target location fique apenas `wwwroot/lib/`, é normal que não apareça a pasta `jqueryui`; os ficheiros ficam diretamente dentro de `wwwroot/lib` (por exemplo `jquery-ui.js` e a pasta `themes`). O importante é referenciar os caminhos corretos no layout.

### 8.2 O que deve aparecer em `wwwroot`

Após a instalação, no `wwwroot/lib` devem existir ficheiros/pastas semelhantes a:

* `jquery-ui.js` (ou `jquery-ui.min.js`)
* `themes/` (com `base/` e/ou temas como *cupertino*, etc.)
* ficheiros CSS do jQuery UI (por exemplo: `themes/base/jquery-ui.css` ou `themes/base/jquery-ui.min.css`)

Também deve existir o ficheiro:

* `libman.json` (na raiz do projeto) — regista o que foi instalado.

### 8.3 Referências no `_Layout.cshtml` (CSS + JS)

Para o jQuery UI funcionar, é obrigatório:

1. **Carregar o CSS do jQuery UI** no `<head>` (antes do `</head>`):

```html
<link rel="stylesheet" href="~/lib/themes/base/jquery-ui.css" />
```

> Se no teu projeto o ficheiro for `jquery-ui.min.css`, usa esse nome.

2. **Carregar o JS do jQuery UI** no fim do `body`, **depois do jQuery**:

```html
<script src="~/lib/jquery/dist/jquery.min.js"></script>
<script src="~/lib/jquery-ui.js"></script>
```

> Regra essencial: **jQuery UI deve ser carregado depois do jQuery**, devido à dependência direta.

### 8.4 Diagnóstico rápido (quando não funciona)

* Se aparecer erro `$ is not defined` → jQuery não foi carregado.
* Se `$.ui` for `undefined` na consola → jQuery UI não foi carregado ou está antes do jQuery.

Teste na consola (F12 → Console):

```js
$.ui
```

Se devolver um objeto (não `undefined`), o jQuery UI está ativo.

---

## 9. O que o tutorial pretende ensinar com jQuery UI

### 9.1 Integração de bibliotecas externas

* Não vêm por defeito no projeto
* Devem ser adicionadas manualmente

### 9.2 Dependências

* jQuery UI **depende do jQuery**
* Deve ser carregado **depois** do jQuery

Erro clássico:

```
$ is not defined
```

### 9.3 Inclusão correta de CSS e JS

* Ficheiros referenciados no `_Layout.cshtml`
* Ordem correta obrigatória

> A referência ao jQuery UI deve surgir sempre após a referência ao jQuery.

---

## 10. Widget Tabs

### 9.1 O que é um Tab Component

* Conjunto de abas
* Clique numa aba muda o conteúdo visível
* Sem recarregar a página

### 9.2 Ativação do widget

```js
$("#mytabs").tabs();
```

Uma única instrução aplica:

* Comportamento
* Layout visual

---

### 9.3 Estrutura obrigatória

* Um `div` principal (`mytabs`)
* Uma lista `<ul>` com links
* Um `<div>` por opção

Correspondência obrigatória:

* `href="#option1"` → `<div id="option1">`

Sem correspondência:

* A aba não funciona

---

### 9.4 Funcionamento interno

1. Clique numa aba
2. Leitura do `href`
3. Esconder todos os conteúdos
4. Mostrar o conteúdo correspondente
5. Atualizar o estado visual

Tudo no lado do cliente.

---

## 10. Autocomplete

### Objetivo

Sugerir valores automaticamente à medida que o utilizador escreve.

### Fonte de dados

* Lista serializada em JSON
* Enviada do controller para a View via `ViewBag`

### Ativação

```js
$("#tags").autocomplete({ source: @ViewBag.tags });
```

---

## 11. AJAX (Countries → Cities)

### Objetivo

* Dois `select`
* O segundo depende do primeiro
* Atualização sem reload

### Processo

1. Utilizador escolhe um país
2. Evento `change`
3. Pedido AJAX (`getJSON`)
4. Controller devolve JSON
5. DOM atualizado dinamicamente

---

## 12. Homework — Authentication Dialog com jQuery UI (Modal Form Dialog)

### Enunciado

Implementar um **Authentication Dialog** (username + password) utilizando o **Modal Form Dialog** do jQuery UI.

### Objetivo técnico

* Abrir um **Dialog modal** (bloqueia o resto da página)
* Mostrar um formulário com **username** e **password**
* Validar credenciais (pode ser simulado, como no tutorial)
* Mostrar mensagem de erro ou fechar o dialog em caso de sucesso

### Implementação sugerida (passo a passo)

#### 12.1 Criar o HTML do Dialog (na View)

* Um botão para abrir o modal
* Um `div` escondido que contém o formulário

Exemplo de estrutura:

* `button#openLogin`
* `div#loginDialog` com `form#loginForm`

#### 12.2 Criar o endpoint no Controller

* Ação `[HttpPost]` que recebe `username` e `password`
* Devolve JSON com `{ success: true/false }`

#### 12.3 Criar o JavaScript

* Inicializar `$("#loginDialog").dialog({ modal: true, ... })`
* No botão *Login* do dialog, submeter o form
* Intercetar o submit e fazer AJAX para o endpoint
* Se `success == true`, fechar o modal e atualizar o estado da página

### Critérios que o professor costuma avaliar

* Integração correta do jQuery UI (CSS + JS)
* Uso do widget `dialog()` em modo **modal**
* Form com campos username/password
* Feedback ao utilizador (mensagem de erro)
* Pedido assíncrono (AJAX) ou validação clara

---

## 13. Conclusão

Este tutorial demonstra como a utilização de jQuery e jQuery UI permite enriquecer significativamente a interação com o utilizador, mantendo os controllers simples e focados na lógica de negócio.

Os conceitos aprendidos (eventos, DOM, AJAX, dependências e integração de bibliotecas) são transversais e aplicáveis a qualquer framework frontend moderna.
