# HelpDeskPro

### Sistema de Gestão de HelpDesk e Suporte Técnico

O **HelpDeskPro** é uma aplicação web desenvolvida para facilitar a gestão de serviços de suporte técnico, permitindo centralizar a informação, organizar pedidos de assistência e gerir utilizadores através de uma plataforma simples e intuitiva.

O projeto foi desenvolvido em C# com ASP.NET Core MVC, utilizando uma base de dados SQLite para armazenar a informação.

---

## 🚀 Funcionalidades

- **Gestão de utilizadores:** sistema de autenticação e gestão de contas.
- **Gestão de tickets:** organização e acompanhamento de pedidos de suporte técnico.
- **Controlo de acessos:** utilização de diferentes funções e permissões.
- **Base de dados:** armazenamento persistente da informação.
- **Interface web:** acesso às funcionalidades através de um navegador.
- **Gestão centralizada:** organização da informação relacionada com o suporte técnico.

---

## 🛠️ Tecnologias utilizadas

| Tecnologia | Finalidade |
|---|---|
| C# | Linguagem de programação principal |
| ASP.NET Core MVC | Desenvolvimento da aplicação web |
| .NET 10 | Plataforma de desenvolvimento |
| Entity Framework Core | Comunicação com a base de dados |
| SQLite | Sistema de gestão de base de dados |
| ASP.NET Core Identity | Autenticação e gestão de utilizadores |
| HTML5 | Estrutura das páginas |
| CSS3 | Estilização e apresentação |
| JavaScript | Funcionalidades interativas |

---

## 📁 Estrutura do projeto

```text
HelpDeskPro/
│
├── Areas/
│   └── Identity/
│       └── Pages/
│
├── Controllers/
│
├── Data/
│
├── Models/
│
├── Properties/
│
├── ViewComponents/
│
├── Views/
│
├── wwwroot/
│
├── Program.cs
├── appsettings.json
├── HelpDeskPro.csproj
└── README.md
```

### Descrição das pastas

- **Areas/Identity:** páginas relacionadas com autenticação e gestão de contas.
- **Controllers:** responsáveis por gerir os pedidos e a lógica de navegação da aplicação.
- **Data:** configuração e acesso à base de dados.
- **Models:** definição das entidades e estruturas de dados.
- **Properties:** configurações de execução.
- **ViewComponents:** componentes reutilizáveis da interface.
- **Views:** páginas e elementos visuais da aplicação.
- **wwwroot:** ficheiros estáticos, como CSS, JavaScript e imagens.

---

## ⚙️ Instalação e execução

### 1. Pré-requisitos

Antes de executar o projeto, é necessário instalar:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) ou Visual Studio Code.
- [Git](https://git-scm.com/).

### 2. Clonar o repositório

Abre o terminal e executa:

```bash
git clone https://github.com/Davidbeca/HelpDeskPro.git
```

### 3. Entrar na pasta do projeto

```bash
cd HelpDeskPro
```

### 4. Restaurar as dependências

```bash
dotnet restore
```

### 5. Executar a aplicação

```bash
dotnet run
```

Após iniciar a aplicação, o terminal apresenta o endereço local onde poderá ser acedida através do navegador.

**Nota:** A aplicação está configurada para aplicar automaticamente as migrações da base de dados e inicializar os dados necessários durante o arranque.

---

## 🗄️ Base de dados

O HelpDeskPro utiliza SQLite para guardar os dados da aplicação.

A integração com a base de dados é realizada através do Entity Framework Core, permitindo gerir as informações através de modelos e operações de acesso a dados.

O sistema também utiliza ASP.NET Core Identity para gerir a autenticação e os diferentes papéis dos utilizadores.

---

## 🎯 Objetivo do projeto

O principal objetivo do HelpDeskPro é desenvolver uma solução informática que permita melhorar a organização e gestão dos serviços de assistência técnica.

A aplicação pretende simplificar o acompanhamento dos pedidos de suporte, centralizar a informação e facilitar o trabalho dos responsáveis pela prestação de assistência.

---

## 👨‍💻 Autor

**David Gonçalves**

GitHub: [@Davidbeca](https://github.com/Davidbeca)

Projeto: [HelpDeskPro](https://github.com/Davidbeca/HelpDeskPro)

---

*HelpDeskPro — Simplificar a gestão do suporte técnico.*
