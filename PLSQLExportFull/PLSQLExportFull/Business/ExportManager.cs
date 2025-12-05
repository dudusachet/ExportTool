using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PLSQLExportFull.Data;
using PLSQLExportFull.Models;
using Oracle.ManagedDataAccess.Client;

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

        // Controle de IDs para substituição no WHERE
        private Int64 minId = Int64.MaxValue;
        private Int64 maxId = 0;
        private Int64 minAutorizacao = Int64.MaxValue;
        private Int64 maxAutorizacao = 0;

        public ExportManager(OracleQueryExecutor queryExecutor, MetadataRepository metadataRepository)
        {
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
            _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
        }

        public void ExportTablesDML(List<TableExportData> tablesToExport, string outputFilePath, string groupname, bool truncate, string servidorInfo)
        {
            if (tablesToExport == null || tablesToExport.Count == 0)
                throw new ArgumentException("Nenhuma tabela selecionada.");

            if (string.IsNullOrEmpty(outputFilePath))
                throw new ArgumentException("Caminho inválido.");

            if (string.IsNullOrEmpty(servidorInfo)) servidorInfo = "N/A";

            StringBuilder dmlScript = new StringBuilder();

            // 1. Cabeçalho Global
            dmlScript.AppendLine("-- Configurações de Ambiente");
            dmlScript.AppendLine("SET ECHO OFF");
            dmlScript.AppendLine("SET FEEDBACK OFF");
            dmlScript.AppendLine("SET VERIFY OFF");
            dmlScript.AppendLine("SET DEFINE OFF");
            dmlScript.AppendLine("SET HEADING OFF");
            dmlScript.AppendLine("SET SQLBLANKLINES ON");
            dmlScript.AppendLine("SET TIMING OFF");
            dmlScript.AppendLine();
            dmlScript.AppendLine($"-- Origem: {servidorInfo}");
            dmlScript.AppendLine($"-- Script: {groupname}");
            dmlScript.AppendLine($"-- Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            dmlScript.AppendLine();

            dmlScript.AppendLine("SET TERMOUT ON");
            dmlScript.AppendLine($"SELECT 'Inicio: ' || TO_CHAR(SYSDATE, 'DD/MM/YYYY HH24:MI:SS') FROM DUAL;");
            dmlScript.AppendLine("SET TERMOUT OFF");
            dmlScript.AppendLine();

            foreach (var table in tablesToExport)
            {
                try
                {
                    // Ajuste do WHERE
                    var novoWhere = table.WhereClause;
                    if (!string.IsNullOrEmpty(novoWhere) && novoWhere.Contains(":MIN"))
                    {
                        novoWhere = novoWhere.Replace(":MIN_ID", minId.ToString())
                                             .Replace(":MAX_ID", maxId.ToString())
                                             .Replace(":MIN_AUTORIZACAO", minAutorizacao.ToString())
                                             .Replace(":MAX_AUTORIZACAO", maxAutorizacao.ToString());
                    }
                    string textoFiltro = string.IsNullOrWhiteSpace(novoWhere) ? "Nenhum Filtro" : novoWhere.Trim();

                    // 2. Busca Dados
                    List<string> insertStatements = _metadataRepository.GetTableDML(
                        table.TableName,
                        novoWhere,
                        table.MinMax,
                        ref minId,
                        ref maxId,
                        ref minAutorizacao,
                        ref maxAutorizacao
                    );

                    int count = insertStatements.Count;

                    // 3. Escreve Cabeçalho da Tabela no Script
                    dmlScript.AppendLine("SET TERMOUT ON");
                    dmlScript.AppendLine("prompt --------------------------------------------------");
                    dmlScript.AppendLine($"SELECT 'Processando {table.TableName}...' FROM DUAL;");
                    dmlScript.AppendLine($"prompt Filtro aplicado: {textoFiltro}");

                    if (truncate)
                    {
                        dmlScript.AppendLine($"prompt [!] Truncating {table.TableName}...");
                        dmlScript.AppendLine($"TRUNCATE TABLE {table.TableName};");
                    }

                    dmlScript.AppendLine($"prompt Registros Gerados no Script: {count}");
                    dmlScript.AppendLine("SET TERMOUT OFF");

                    // 4. Gera INSERTs
                    if (count > 0)
                    {
                        int rowCount = 0;
                        dmlScript.AppendLine();
                        foreach (string insert in insertStatements)
                        {
                            dmlScript.AppendLine(insert);
                            rowCount++;
                            if (rowCount % 100 == 0) dmlScript.AppendLine("commit;");
                        }
                        dmlScript.AppendLine("commit;");
                        dmlScript.AppendLine();
                        dmlScript.AppendLine("SET TERMOUT ON");
                        dmlScript.AppendLine($"SELECT 'VALIDACAO {table.TableName}: Esperado:' || {count} || ' | Encontrado:' || COUNT(1) FROM {table.TableName};");
                        dmlScript.AppendLine("SET TERMOUT OFF");
                    }
                    else
                    {
                        dmlScript.AppendLine($"-- Tabela vazia ou sem dados.");
                        dmlScript.AppendLine("SET TERMOUT ON");
                        dmlScript.AppendLine("prompt Tabela vazia (0 registros gerados).");
                        dmlScript.AppendLine("SET TERMOUT OFF");
                    }
                    dmlScript.AppendLine();
                }
                catch (Exception ex)
                {
                    dmlScript.AppendLine("SET TERMOUT ON");
                    dmlScript.AppendLine($"prompt ERRO NO C# AO PROCESSAR {table.TableName}: {ex.Message}");
                    dmlScript.AppendLine("SET TERMOUT OFF");
                    dmlScript.AppendLine($"-- Erro Detalhado: {ex.ToString()}");
                    dmlScript.AppendLine();
                }
            }

            // Rodapé
            dmlScript.AppendLine("SET TERMOUT ON");
            dmlScript.AppendLine("prompt --------------------------------------------------");
            dmlScript.AppendLine("SELECT 'Fim: ' || TO_CHAR(SYSDATE, 'DD/MM/YYYY HH24:MI:SS') FROM DUAL;");
            dmlScript.AppendLine("prompt --------------------------------------------------");

            File.WriteAllText(outputFilePath, dmlScript.ToString(), Encoding.UTF8);
        }
    }
}