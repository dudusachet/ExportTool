using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PLSQLExportTool.Data;
using PLSQLExportTool.Models;

namespace PLSQLExportTool.Business
{
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
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
            _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
        }

        /// <summary>
        /// Exporta DDL de tabelas selecionadas
        /// </summary>
        /// <param name="tableNames">Lista de nomes de tabelas</param>
        /// <param name="outputFilePath">Caminho do arquivo de saída</param>
        public void ExportTablesDML(List<(string TableName, string WhereClause, string MinMax)> tablesToExport, string outputFilePath)
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

                    // Ajuste: MetadataRepository tem GetTableDML(string). Usa-se a versão existente.
                    List<string> insertStatements = _metadataRepository.GetTableDML(table.TableName, novoWhere, table.MinMax, ref minId, ref maxId);

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
                            if (count % 100 == 0)
                            {
                                dmlScript.AppendLine($"commit;");
                            }
                        }
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
        /// <param name="constraints">Lista de constraints</param>
        /// <param name="outputFilePath">Caminho do arquivo de saída</param>
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