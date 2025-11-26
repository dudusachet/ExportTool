using System;
using System.Collections.Generic;
using PLSQLExportFull.Data;
using PLSQLExportFull.Models;

namespace PLSQLExportFull.Business
{
    /// <summary>
    /// Gerencia operações com triggers do banco de dados
    /// </summary>
    public class TriggerManager
    {
        private OracleQueryExecutor _queryExecutor;
        private MetadataRepository _metadataRepository;

        /// <summary>
        /// Construtor
        /// </summary>
        /// <param name="queryExecutor">Executor de queries</param>
        /// <param name="metadataRepository">Repositório de metadados</param>
        public TriggerManager(OracleQueryExecutor queryExecutor, MetadataRepository metadataRepository)
        {
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
            _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
        }

        /// <summary>
        /// Obtém lista de todas as triggers
        /// </summary>
        /// <returns>Lista de TriggerInfo</returns>
        public List<TriggerInfo> GetAllTriggers()
        {
            return _metadataRepository.GetAllTriggers();
        }

        /// <summary>
        /// Desabilita uma trigger
        /// </summary>
        /// <param name="triggerName">Nome da trigger</param>
        public void DisableTrigger(string triggerName)
        {
            if (string.IsNullOrEmpty(triggerName))
            {
                throw new ArgumentException("Nome da trigger não pode ser vazio.", nameof(triggerName));
            }

            string command = $"ALTER TRIGGER {triggerName} DISABLE";
            _queryExecutor.ExecuteNonQuery(command);
        }

        /// <summary>
        /// Habilita uma trigger
        /// </summary>
        /// <param name="triggerName">Nome da trigger</param>
        public void EnableTrigger(string triggerName)
        {
            if (string.IsNullOrEmpty(triggerName))
            {
                throw new ArgumentException("Nome da trigger não pode ser vazio.", nameof(triggerName));
            }

            string command = $"ALTER TRIGGER {triggerName} ENABLE";
            _queryExecutor.ExecuteNonQuery(command);
        }

        /// <summary>
        /// Desabilita múltiplas triggers
        /// </summary>
        /// <param name="triggerNames">Lista de nomes de triggers</param>
        /// <returns>Número de triggers desabilitadas com sucesso</returns>
        public int DisableTriggers(List<string> triggerNames)
        {
            if (triggerNames == null || triggerNames.Count == 0)
            {
                return 0;
            }

            int successCount = 0;
            List<string> errors = new List<string>();

            foreach (string triggerName in triggerNames)
            {
                try
                {
                    DisableTrigger(triggerName);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Erro ao desabilitar trigger {triggerName}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                throw new Exception($"Algumas triggers não puderam ser desabilitadas:\n{string.Join("\n", errors)}");
            }

            return successCount;
        }

        /// <summary>
        /// Habilita múltiplas triggers
        /// </summary>
        /// <param name="triggerNames">Lista de nomes de triggers</param>
        /// <returns>Número de triggers habilitadas com sucesso</returns>
        public int EnableTriggers(List<string> triggerNames)
        {
            if (triggerNames == null || triggerNames.Count == 0)
            {
                return 0;
            }

            int successCount = 0;
            List<string> errors = new List<string>();

            foreach (string triggerName in triggerNames)
            {
                try
                {
                    EnableTrigger(triggerName);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Erro ao habilitar trigger {triggerName}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                throw new Exception($"Algumas triggers não puderam ser habilitadas:\n{string.Join("\n", errors)}");
            }

            return successCount;
        }

        /// <summary>
        /// Desabilita todas as triggers de uma tabela
        /// </summary>
        /// <param name="tableName">Nome da tabela</param>
        public void DisableAllTableTriggers(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Nome da tabela não pode ser vazio.", nameof(tableName));
            }

            string command = $"ALTER TABLE {tableName} DISABLE ALL TRIGGERS";
            _queryExecutor.ExecuteNonQuery(command);
        }

        /// <summary>
        /// Habilita todas as triggers de uma tabela
        /// </summary>
        /// <param name="tableName">Nome da tabela</param>
        public void EnableAllTableTriggers(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Nome da tabela não pode ser vazio.", nameof(tableName));
            }

            string command = $"ALTER TABLE {tableName} ENABLE ALL TRIGGERS";
            _queryExecutor.ExecuteNonQuery(command);
        }
    }
}
