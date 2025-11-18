using System;
using System.Collections.Generic;
using PLSQLExportTool.Data;
using PLSQLExportTool.Models;

namespace PLSQLExportTool.Business
{
    /// <summary>
    /// Gerencia operações com constraints do banco de dados
    /// </summary>
    public class ConstraintManager
    {
        private OracleQueryExecutor _queryExecutor;
        private MetadataRepository _metadataRepository;

        /// <summary>
        /// Construtor
        /// </summary>
        /// <param name="queryExecutor">Executor de queries</param>
        /// <param name="metadataRepository">Repositório de metadados</param>
        public ConstraintManager(OracleQueryExecutor queryExecutor, MetadataRepository metadataRepository)
        {
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
            _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
        }

        /// <summary>
        /// Obtém lista de todas as constraints
        /// </summary>
        /// <returns>Lista de ConstraintInfo</returns>
        public List<ConstraintInfo> GetAllConstraints()
        {
            return _metadataRepository.GetAllConstraints();
        }

        /// <summary>
        /// Desabilita uma constraint
        /// </summary>
        /// <param name="constraint">Informações da constraint</param>
        public void DisableConstraint(ConstraintInfo constraint)
        {
            if (constraint == null)
            {
                throw new ArgumentNullException(nameof(constraint));
            }

            if (string.IsNullOrEmpty(constraint.TableName) || string.IsNullOrEmpty(constraint.ConstraintName))
            {
                throw new ArgumentException("Nome da tabela e constraint não podem ser vazios.");
            }

            string command = $"ALTER TABLE {constraint.TableName} DISABLE CONSTRAINT {constraint.ConstraintName}";
            _queryExecutor.ExecuteNonQuery(command);
        }

        /// <summary>
        /// Habilita uma constraint
        /// </summary>
        /// <param name="constraint">Informações da constraint</param>
        public void EnableConstraint(ConstraintInfo constraint)
        {
            if (constraint == null)
            {
                throw new ArgumentNullException(nameof(constraint));
            }

            if (string.IsNullOrEmpty(constraint.TableName) || string.IsNullOrEmpty(constraint.ConstraintName))
            {
                throw new ArgumentException("Nome da tabela e constraint não podem ser vazios.");
            }

            string command = $"ALTER TABLE {constraint.TableName} ENABLE CONSTRAINT {constraint.ConstraintName}";
            _queryExecutor.ExecuteNonQuery(command);
        }

        /// <summary>
        /// Desabilita múltiplas constraints
        /// </summary>
        /// <param name="constraints">Lista de constraints</param>
        /// <returns>Número de constraints desabilitadas com sucesso</returns>
        public int DisableConstraints(List<ConstraintInfo> constraints)
        {
            if (constraints == null || constraints.Count == 0)
            {
                return 0;
            }

            int successCount = 0;
            List<string> errors = new List<string>();

            foreach (ConstraintInfo constraint in constraints)
            {
                try
                {
                    DisableConstraint(constraint);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Erro ao desabilitar constraint {constraint.ConstraintName}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                throw new Exception($"Algumas constraints não puderam ser desabilitadas:\n{string.Join("\n", errors)}");
            }

            return successCount;
        }

        /// <summary>
        /// Habilita múltiplas constraints
        /// </summary>
        /// <param name="constraints">Lista de constraints</param>
        /// <returns>Número de constraints habilitadas com sucesso</returns>
        public int EnableConstraints(List<ConstraintInfo> constraints)
        {
            if (constraints == null || constraints.Count == 0)
            {
                return 0;
            }

            int successCount = 0;
            List<string> errors = new List<string>();

            foreach (ConstraintInfo constraint in constraints)
            {
                try
                {
                    EnableConstraint(constraint);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Erro ao habilitar constraint {constraint.ConstraintName}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                throw new Exception($"Algumas constraints não puderam ser habilitadas:\n{string.Join("\n", errors)}");
            }

            return successCount;
        }

        /// <summary>
        /// Desabilita todas as constraints de uma tabela
        /// </summary>
        /// <param name="tableName">Nome da tabela</param>
        public void DisableAllTableConstraints(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Nome da tabela não pode ser vazio.", nameof(tableName));
            }

            // Obter todas as constraints da tabela
            List<ConstraintInfo> allConstraints = GetAllConstraints();
            List<ConstraintInfo> tableConstraints = allConstraints.FindAll(c => c.TableName == tableName);

            DisableConstraints(tableConstraints);
        }

        /// <summary>
        /// Habilita todas as constraints de uma tabela
        /// </summary>
        /// <param name="tableName">Nome da tabela</param>
        public void EnableAllTableConstraints(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Nome da tabela não pode ser vazio.", nameof(tableName));
            }

            // Obter todas as constraints da tabela
            List<ConstraintInfo> allConstraints = GetAllConstraints();
            List<ConstraintInfo> tableConstraints = allConstraints.FindAll(c => c.TableName == tableName);

            EnableConstraints(tableConstraints);
        }
    }
}
