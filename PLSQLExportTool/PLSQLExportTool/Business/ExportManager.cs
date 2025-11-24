using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PLSQLExportTool.Data;
using PLSQLExportTool.Models;

namespace PLSQLExportTool.Business
{
    // ==========================================================================
    // CLASSE AUXILIAR (Necessária para substituir a Tupla no .NET 4.0)
    // ==========================================================================
    public class TableExportData
    {
        public string TableName { get; set; }
        public string WhereClause { get; set; }
        public string MinMax { get; set; }
    }

    /// <summary>
    /// Gerencia exportação de estruturas de banco de dados
    /// </summary>
    public class ExportManager
    {
        private OracleQueryExecutor _queryExecutor;
        private MetadataRepository _metadataRepository;
        private Int64 minId = Int64.MaxValue;
        private Int64 maxId = 0;

        /// <summary>
        /// Construtor
        /// </summary>
        /// <param name="queryExecutor">Executor de queries</param>
        /// <param name="metadataRepository">Repositório de metadados</param>
        public ExportManager(OracleQueryExecutor queryExecutor, MetadataRepository metadataRepository)
        {
            // No .NET 4.0, nameof() funciona se usar VS mais novo para compilar, 
            // se der erro, troque por string "queryExecutor"
            if (queryExecutor == null) throw new ArgumentNullException("queryExecutor");
            if (metadataRepository == null) throw new ArgumentNullException("metadataRepository");

            _queryExecutor = queryExecutor;
            _metadataRepository = metadataRepository;
        }

        /// <summary>
        /// Exporta DML de tabelas selecionadas
        /// </summary>
        /// <param name="tablesToExport">Lista de dados das tabelas (Classe em vez de Tupla)</param>
        /// <param name="outputFilePath">Caminho do arquivo de saída</param>
        public void ExportTablesDML(List<TableExportData> tablesToExport, string outputFilePath)
        {
            if (tablesToExport == null || tablesToExport.Count == 0)
            {
                throw new ArgumentException("Nenhuma tabela selecionada para exportação.");
            }

            if (string.IsNullOrEmpty(outputFilePath))
            {
                throw new ArgumentException("Caminho do arquivo de saída não pode ser vazio.");
            }

            StringBuilder dmlScript = new StringBuilder();

            // Cabeçalho do script
            dmlScript.AppendLine("-- ========================================");
            dmlScript.AppendLine("-- Script de Exportação DML (INSERT)");
            // Interpolação de string ($"...") funciona no compilador moderno mesmo visando .NET 4.0.
            // Se der erro, use string.Format("-- Gerado em: {0}", DateTime.Now);
            dmlScript.AppendLine($"-- Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            dmlScript.AppendLine($"-- Tabelas: {tablesToExport.Count}");
            dmlScript.AppendLine("-- ========================================");
            dmlScript.AppendLine();

            // Exportar cada tabela
            foreach (var table in tablesToExport)
            {
                try
                {
                    var novoWhere = table.WhereClause;
                    dmlScript.AppendLine($"-- ========================================");
                    dmlScript.AppendLine($"-- Tabela: {table.TableName}");

                    if (!string.IsNullOrEmpty(novoWhere))
                    {
                        if (novoWhere.Contains(":MIN"))
                        {
                            novoWhere = novoWhere.Replace(":MIN_ID", minId.ToString());
                            novoWhere = novoWhere.Replace(":MAX_ID", maxId.ToString());
                        }

                        dmlScript.AppendLine($"-- Filtro WHERE: {novoWhere}");
                    }
                    dmlScript.AppendLine($"-- ========================================");
                    dmlScript.AppendLine();

                    // Mantendo sua lógica original que chama o repositório
                    // Agora acessando as propriedades da classe (.TableName, .MinMax)
                    List<string> insertStatements = _metadataRepository.GetTableDML(
                        table.TableName,
                        novoWhere,
                        table.MinMax,
                        ref minId,
                        ref maxId
                    );

                    if (insertStatements.Count <= 0)
                    {
                        dmlScript.AppendLine("-- Nenhuma linha encontrada para exportação.");
                        dmlScript.AppendLine();
                    }
                    else
                    {
                        var count = 0;
                        dmlScript.AppendLine($"-- {insertStatements.Count} registros encontrados");

                        foreach (string insert in insertStatements)
                        {
                            dmlScript.AppendLine(insert);
                            count++;
                            // Commit a cada 100 registros
                            if (count % 100 == 0)
                            {
                                dmlScript.AppendLine($"commit;");
                            }
                        }
                        // Commit final da tabela
                        dmlScript.AppendLine($"commit;");
                        dmlScript.AppendLine();
                    }
                }
                catch (Exception ex)
                {
                    dmlScript.AppendLine($"-- Erro ao exportar tabela {table.TableName}: {ex.Message}");
                    dmlScript.AppendLine();
                }
            }

            // Rodapé do script
            dmlScript.AppendLine("-- ========================================");
            dmlScript.AppendLine("-- Fim do Script");
            dmlScript.AppendLine("-- ========================================");

            // Salvar arquivo
            File.WriteAllText(outputFilePath, dmlScript.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Gera script para reabilitar constraints
        /// </summary>
        public void GenerateEnableConstraintsScript(List<ConstraintInfo> constraints, string outputFilePath)
        {
            if (constraints == null || constraints.Count == 0)
            {
                throw new ArgumentException("Nenhuma constraint selecionada.");
            }

            if (string.IsNullOrEmpty(outputFilePath))
            {
                throw new ArgumentException("Caminho do arquivo de saída não pode ser vazio.");
            }

            StringBuilder script = new StringBuilder();

            script.AppendLine("-- ========================================");
            script.AppendLine("-- Script para Reabilitar Constraints");
            script.AppendLine($"-- Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            script.AppendLine("-- ========================================");
            script.AppendLine();

            foreach (ConstraintInfo constraint in constraints)
            {
                script.AppendLine($"-- {constraint.ConstraintName} ({constraint.ConstraintTypeDescription})");
                script.AppendLine($"ALTER TABLE {constraint.TableName} ENABLE CONSTRAINT {constraint.ConstraintName};");
                script.AppendLine();
            }

            File.WriteAllText(outputFilePath, script.ToString(), Encoding.UTF8);
        }
    }
}