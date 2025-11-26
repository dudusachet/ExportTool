# PLSQL Migration Suite - Export Tool (PLSQLExportTool)

## Visão Geral

O **PLSQLExportTool** é uma ferramenta desktop desenvolvida em C# (.NET Framework 4.0) projetada para facilitar a exportação de dados (DML - Data Manipulation Language) de tabelas Oracle. É ideal para cenários de migração ou para a criação de scripts de carga de dados.

A ferramenta permite a seleção de tabelas, aplicação de filtros WHERE customizados e a geração de scripts SQL prontos para execução no ambiente Oracle (SQL*Plus/SQLcl).

## Funcionalidades Principais

*   **Exportação DML:** Gera scripts SQL contendo comandos `INSERT` para as tabelas selecionadas.
*   **Compatibilidade:** Projetado para rodar em ambientes com **.NET Framework 4.0** (compatível com Visual Studio 2019).
*   **Agrupamento de Tabelas:** Suporte a grupos de tabelas predefinidos via arquivo `TableGroups.json`, permitindo a exportação de conjuntos de dados relacionados.
*   **Filtros WHERE Customizados:** Permite a aplicação de cláusulas `WHERE` manuais para exportação seletiva de dados.
*   **Usabilidade:** Implementação de cursor de espera (`WaitCursor`) durante a exportação para melhor experiência do usuário.
*   **Scripts Otimizados:**
    *   Inclusão de comandos SQL*Plus/SQLcl (`SET ECHO OFF`, `SET FEEDBACK OFF`, etc.) para execução limpa.
    *   Inclusão de *timestamps* de início e fim (global e por tabela) para monitoramento do processo de importação.

## Requisitos

*   **Ambiente de Desenvolvimento:** Visual Studio 2019 (ou superior, com suporte a .NET Framework 4.0).
*   **Runtime:** .NET Framework 4.0.
*   **Banco de Dados:** Acesso a um banco de dados Oracle.
*   **Dependências:**
    *   `Oracle.ManagedDataAccess` (versão 19.21.0)
    *   `Newtonsoft.Json` (para manipulação do arquivo de grupos)

## Como Usar

### 1. Configuração e Conexão

1.  Preencha os campos de **Host**, **Port**, **Service Name**, **User ID** e **Password** na aba de Conexão.
2.  Clique em **Testar Conexão** para verificar a conectividade.
3.  Clique em **Conectar** para estabelecer a conexão com o banco de dados.

### 2. Carregamento e Seleção de Tabelas

1.  Na aba de Exportação, selecione um grupo de tabelas no `cmbTableGroups` (ou mantenha "Todos").
2.  Clique em **Atualizar Tabelas** para carregar a lista de tabelas disponíveis.
3.  Selecione as tabelas que deseja exportar na lista.

### 3. Exportação DML

1.  (Opcional) Se "Todos" estiver selecionado, você pode inserir uma cláusula `WHERE` manual no campo de filtro.
2.  Clique em **Exportar DML**.
3.  Escolha o local e o nome do arquivo `.sql` de saída.
4.  A aplicação exibirá um cursor de espera e desabilitará o botão durante o processamento.
5.  Ao final, uma mensagem de sucesso será exibida.

## Estrutura do Projeto

*   `PLSQLExportTool.csproj`: Arquivo de projeto configurado para .NET Framework 4.0.
*   `packages.config`: Gerenciamento de dependências (incluindo `Oracle.ManagedDataAccess` e `Newtonsoft.Json`).
*   `Forms/ExportForm.cs`: Lógica da interface do usuário, incluindo a manipulação do cursor de espera.
*   `Business/ExportManager.cs`: Lógica central de geração do script SQL, incluindo os comandos `SET` e os *timestamps*.
*   `TableGroups.json`: Arquivo de configuração para definir grupos de tabelas e seus filtros predefinidos.
