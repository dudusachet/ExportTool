using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PLSQLExportFull.Data;
using PLSQLExportFull.Models;

namespace PLSQLExportFull.Business
{
    public class TableExportData
    {
        public string TableName { get; set; }
        public string WhereClause { get; set; }
        public string MinMax { get; set; }
    }

    public class ExportManager
    {
        private OracleQueryExecutor _queryExecutor;
        private MetadataRepository _metadataRepository;

        // Controle de IDs para cláusulas WHERE dinâmicas
        private Int64 minId = Int64.MaxValue;
        private Int64 maxId = 0;
        private Int64 minAutorizacao = Int64.MaxValue;
        private Int64 maxAutorizacao = 0;

        public ExportManager(OracleQueryExecutor queryExecutor, MetadataRepository metadataRepository)
        {
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
            _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
        }

        public void ExportTablesDML(List<TableExportData> tablesToExport, string outputFilePath, string groupname, bool truncate, string servidor)
        {
            if (tablesToExport == null || tablesToExport.Count == 0)
                throw new ArgumentException("Nenhuma tabela selecionada para exportação.");

            if (string.IsNullOrEmpty(outputFilePath))
                throw new ArgumentException("Caminho do arquivo de saída não pode ser vazio.");

            StringBuilder dmlScript = new StringBuilder();

            // ---------------------------------------------------------
            // 1. Cabeçalho e Configurações Globais
            // ---------------------------------------------------------
            dmlScript.AppendLine("-- Configurações de Ambiente");
            dmlScript.AppendLine("SET ECHO OFF");
            dmlScript.AppendLine("SET FEEDBACK OFF");
            dmlScript.AppendLine("SET VERIFY OFF");
            dmlScript.AppendLine("SET DEFINE OFF");
            dmlScript.AppendLine("SET HEADING OFF");
            dmlScript.AppendLine("SET SQLBLANKLINES ON");
            dmlScript.AppendLine("SET TIMING OFF");
            dmlScript.AppendLine();

            dmlScript.AppendLine($"-- Origem: {servidor}");
            dmlScript.AppendLine($"-- Script: {groupname}");
            dmlScript.AppendLine($"-- Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            dmlScript.AppendLine($"-- Tabelas: {tablesToExport.Count}");
            dmlScript.AppendLine();

            // Log de Início Geral
            dmlScript.AppendLine("SET TERMOUT ON");
            // Mostra o nome do banco na tela do SQL Plus também
            dmlScript.AppendLine($"SELECT 'Inicio: ' || TO_CHAR(SYSDATE, 'DD/MM/YYYY HH24:MI:SS') FROM DUAL;");
            dmlScript.AppendLine("SET TERMOUT OFF");
            dmlScript.AppendLine();

            foreach (var table in tablesToExport)
            {
                try
                {
                    var novoWhere = table.WhereClause;
                    if (!string.IsNullOrEmpty(novoWhere))
                    {
                        if (novoWhere.Contains(":MIN"))
                        {
                            novoWhere = novoWhere.Replace(":MIN_ID", minId.ToString())
                                                 .Replace(":MAX_ID", maxId.ToString())
                                                 .Replace(":MIN_AUTORIZACAO", minAutorizacao.ToString())
                                                 .Replace(":MAX_AUTORIZACAO", maxAutorizacao.ToString());
                        }
                    }

                    // ---------------------------------------------------------
                    // 2. BUSCA DADOS
                    // ---------------------------------------------------------
                    List<string> insertStatements = _metadataRepository.GetTableDML(
                        table.TableName,
                        novoWhere,
                        table.MinMax,
                        ref minId,
                        ref maxId,
                        ref minAutorizacao,
                        ref maxAutorizacao
                    );

                    // ---------------------------------------------------------
                    // 3. Escreve o Bloco de Log (Visível)
                    // ---------------------------------------------------------
                    dmlScript.AppendLine("SET TERMOUT ON");
                    dmlScript.AppendLine("prompt --------------------------------------------------------------------------------");
                    dmlScript.AppendLine();
                    dmlScript.AppendLine($"SELECT 'Tabela {table.TableName}: ' || TO_CHAR(SYSDATE, 'HH24:MI:SS') FROM DUAL;");

                    // --- LÓGICA DO TRUNCATE ---
                    if (truncate)
                    {
                        dmlScript.AppendLine($"prompt [!] Truncating {table.TableName}...");
                        dmlScript.AppendLine($"TRUNCATE TABLE {table.TableName};");
                    }
                    // --------------------------

                    dmlScript.AppendLine($"prompt Processando: {table.TableName} - {insertStatements.Count} registros encontrados");
                    dmlScript.AppendLine($"prompt Filtro:{table.WhereClause}");
                    dmlScript.AppendLine();
                    dmlScript.AppendLine($"---Filtro aplicado:{table.WhereClause}");
                    dmlScript.AppendLine("SET TERMOUT OFF");

                    // ---------------------------------------------------------
                    // 4. Escreve os Inserts (Invisíveis)
                    // ---------------------------------------------------------
                    if (insertStatements.Count > 0)
                    {
                        var count = 0;
                        dmlScript.AppendLine();

                        foreach (string insert in insertStatements)
                        {
                            dmlScript.AppendLine(insert);
                            count++;

                            if (count % 100 == 0)
                            {
                                dmlScript.AppendLine("commit;");
                            }
                        }
                        dmlScript.AppendLine("commit;");
                    }
                    else
                    {
                        dmlScript.AppendLine($"-- Tabela {table.TableName} vazia ou sem dados no filtro.");
                    }

                    dmlScript.AppendLine(); // Espaço entre tabelas
                }
                catch (Exception ex)
                {
                    dmlScript.AppendLine("SET TERMOUT ON");
                    dmlScript.AppendLine($"prompt ERRO AO PROCESSAR TABELA {table.TableName}: {ex.Message}");
                    dmlScript.AppendLine("SET TERMOUT OFF");
                    dmlScript.AppendLine($"-- Detalhe: {ex.ToString()}");
                    dmlScript.AppendLine();
                }
            }

            // ---------------------------------------------------------
            // 5. Log de Fim Geral
            // ---------------------------------------------------------
            dmlScript.AppendLine("SET TERMOUT ON");
            dmlScript.AppendLine("prompt --------------------------------------------------------------------------------");
            dmlScript.AppendLine();
            dmlScript.AppendLine("SELECT 'Fim do processo: ' || TO_CHAR(SYSDATE, 'DD/MM/YYYY HH24:MI:SS') FROM DUAL;");
            dmlScript.AppendLine("prompt --------------------------------------------------------------------------------");
            dmlScript.AppendLine();

            // Grava o arquivo
            File.WriteAllText(outputFilePath, dmlScript.ToString(), Encoding.UTF8);
        }

        public void GenerateEnableConstraintsScript(List<ConstraintInfo> constraints, string outputFilePath)
        {
            if (constraints == null || constraints.Count == 0) return;

            StringBuilder script = new StringBuilder();
            script.AppendLine("-- ========================================");
            script.AppendLine($"-- Reabilitar Constraints - {DateTime.Now}");
            script.AppendLine("-- ========================================");
            script.AppendLine();

            foreach (ConstraintInfo constraint in constraints)
            {
                script.AppendLine($"ALTER TABLE {constraint.TableName} ENABLE CONSTRAINT {constraint.ConstraintName};");
            }

            File.WriteAllText(outputFilePath, script.ToString(), Encoding.UTF8);
        }
    }
}