# Tijori - A CRM Application 

An enterprise-grade Desktop Customer Relationship Management (CRM) application built using the modern .NET 8 framework, WPF, and MySQL. This application is designed with high-performance desktop architecture, utilizing the MVVM (Model-View-ViewModel) design pattern to ensure scalability, clean separation of concerns, and robust database synchronization logic.

## 🚀 Features

- **Customer & Lead Management:** Efficiently track client data, communication histories, and pipelines.
- **Robust Database Sync:** Real-time and offline-resilient data synchronization logic powered by a MySQL backend.
- **Modern UI/UX:** A responsive, clean, and intuitive desktop interface built with Windows Presentation Foundation (WPF).
- **Enterprise Architecture:** Structured using MVVM for clean code maintainability, testability, and decoupled UI-to-backend logic.

## 🛠️ Technology Stack

- **Framework:** .NET 8.0 (LTS)
- **Presentation Layer:** WPF (Windows Presentation Foundation) / XAML
- **Language:** C# 12
- **Database:** MySQL
- **Architecture Pattern:** MVVM (Model-View-ViewModel)

## 📋 Prerequisites

Before running or building the project, ensure you have the following installed:

- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (with *.NET Desktop Development* workload checked)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- [MySQL Server 8.0+](https://dev.mysql.com/downloads/mysql/) or a cloud-hosted MySQL instance

## ⚙️ Getting Started

### 1. Clone the Repository
```bash
git clone [https://github.com/pun12189/CRM-App.git](https://github.com/pun12189/CRM-App.git)
cd CRM-App
```
### Build and Run
* Open the solution file (CRM-App.sln) in Visual Studio 2022.
* Let NuGet restore all required dependencies automatically.
* Set the main WPF project as the Startup Project.
* Press F5 or click Start to build and run the application in Debug mode.

### Database Configuration
```bash
<connectionStrings>
    <add name="CRMConnectionString" 
         connectionString="Server=localhost;Database=your_crm_db;Uid=your_username;Pwd=your_password;" 
         providerName="MySql.Data.MySqlClient" />
</connectionStrings>
```

## 🗂️ Project Structure
CRM-App /
- 📁 Models/ — Data structures, domain entities, and business logic data objects.
- 📁 Views/ — User interface components, XAML windows, pages, and user controls.
- 📁 ViewModels/ — UI logic state, data bindings, and application command handling.
- 📁 Data/ — Database context configurations, repositories, and backend sync logic.
- 📁 Helpers/ — Custom value converters, utility functions, and string extensions.

### Key Highlights Included:
* **Architecture-focused:** It emphasizes **MVVM**, high performance, and clean separation of concerns.
* **Tech Specifications:** Explicitly calls out **.NET 8**, **C#**, **WPF**, and **MySQL**.
* **Setup Instructions:** Provides explicit, standard workflow steps for desktop developers (Visual Studio 2022, configuration setups, and NuGet restorations). 

Feel free to tweak the specific features or directory paths as your codebase evolves!
