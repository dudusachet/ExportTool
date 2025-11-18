using System;
using System.Collections.Generic;
using PLSQLExportTool.Data;
using PLSQLExportTool.Models;

namespace PLSQLExportTool.Business
{
    /// <summary>
    /// Gerencia operações com tabelas do banco de dados
    /// </summary>
    public class TableManager
    {
        private OracleQueryExecutor _queryExecutor;
        private MetadataRepository _metadataRepository;

        /// <summary>
        /// Construtor
        /// </summary>
        /// <param name="queryExecutor">Executor de queries</param>
        /// <param name="metadataRepository">Repositório de metadados</param>
        public TableManager(OracleQueryExecutor queryExecutor, MetadataRepository metadataRepository)
        {
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
            _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
        }

        /// <summary>
        /// Obtém lista de todas as tabelas
        /// </summary>
        /// <returns>Lista de TableInfo</returns>
        public List<TableInfo> GetAllTables()
        {
            return _metadataRepository.GetAllTables();
        }

        /// <summary>
        /// Trunca uma tabela
        /// </summary>
        /// <param name="tableName">Nome da tabela</param>
        public void TruncateTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Nome da tabela não pode ser vazio.", nameof(tableName));
            }

            string command = $"TRUNCATE TABLE {tableName}";
            _queryExecutor.ExecuteNonQuery(command);
        }

        /// <summary>
        /// Trunca múltiplas tabelas
        /// </summary>
        /// <param name="tableNames">Lista de nomes de tabelas</param>
        /// <returns>Número de tabelas truncadas com sucesso</returns>
        public int TruncateTables(List<string> tableNames)
        {
            if (tableNames == null || tableNames.Count == 0)
            {
                return 0;
            }

            int successCount = 0;
            List<string> errors = new List<string>();

            foreach (string tableName in tableNames)
            {
                try
                {
                    TruncateTable(tableName);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Erro ao truncar tabela {tableName}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                throw new Exception($"Algumas tabelas não puderam ser truncadas:\n{string.Join("\n", errors)}");
            }

            return successCount;
        }

        /// <summary>
        /// Deleta todos os dados de uma tabela (alternativa ao truncate quando há constraints)
        /// </summary>
        /// <param name="tableName">Nome da tabela</param>
        public void DeleteAllFromTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Nome da tabela não pode ser vazio.", nameof(tableName));
            }

            string command = $"DELETE FROM {tableName}";
            _queryExecutor.ExecuteNonQuery(command);
        }

        /// <summary>
        /// Obtém contagem de registros de uma tabela
        /// </summary>
        /// <param name="tableName">Nome da tabela</param>
        /// <returns>Número de registros</returns>
        public long GetTableRowCount(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Nome da tabela não pode ser vazio.", nameof(tableName));
            }

            string query = $"SELECT COUNT(*) FROM {tableName}";
            object result = _queryExecutor.ExecuteScalar(query);
            
            if (result != null && result != DBNull.Value)
            {
                return Convert.ToInt64(result);
            }

            return 0;
        }

        /// <summary>
        /// Verifica se uma tabela existe
        /// </summary>
        /// <param name="tableName">Nome da tabela</param>
        /// <returns>True se a tabela existe</returns>
        public bool TableExists(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return false;
            }

            string query = $"SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = '{tableName.ToUpper()}'";
            object result = _queryExecutor.ExecuteScalar(query);
            
            if (result != null && result != DBNull.Value)
            {
                return Convert.ToInt32(result) > 0;
            }

            return false;
        }
    }
}
