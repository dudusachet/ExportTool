using System;
using System.Collections.Generic;
using System.Data;
using PLSQLExportFull.Models;
using System.Globalization;
using System.Text;

namespace PLSQLExportFull.Data
{
    /// <summary>
    /// Repositório para consultas ao dicionário de dados Oracle
    /// </summary>
    public class MetadataRepository
    {
        private OracleQueryExecutor _queryExecutor;

        // ========================================================================
        // BLACKLIST LGPD - DEFINIÇÃO DE CAMPOS SENSÍVEIS
        // ========================================================================
        private static readonly Dictionary<string, HashSet<string>> _sensitiveColumns = new Dictionary<string, HashSet<string>>
        {
            { "PEDIDOS",                    new HashSet<string> { "CNPJ", "NOME_REPRES", "NOME_CLIENTE", "ENDERECO", "FONE" } },
            { "PEDIDOS_ERP",                new HashSet<string> { "CNPJ", "NOME_CLIENTE", "ENDERECO", "FONE" } },
            { "RESERVA",                    new HashSet<string> { "CNPJ", "NOME_CLIENTE" } },
            { "WMS_CLIENTES",               new HashSet<string> { "CNPJ", "NOME" } },
            { "WMS_COLABORADORES",          new HashSet<string> { "NOME" } },
            { "GER_USUARIOS",               new HashSet<string> { "NOMECOMPLETO", "EMAIL" } },
            { "WMS_MOTORISTAS",               new HashSet<string> { "PLACA", "NOME", "FONE", "NOME","NUMERO_CNH" } }
        };

        /// <summary>
        /// Construtor
        /// </summary>
        public MetadataRepository(OracleQueryExecutor queryExecutor)
        {
            _queryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
        }

        /// <summary>
        /// Obtém lista de todas as triggers do usuário
        /// </summary>
        public List<TriggerInfo> GetAllTriggers()
        {
            string query = @"
                SELECT TRIGGER_NAME, TABLE_NAME, STATUS, TRIGGER_TYPE, TRIGGERING_EVENT
                FROM USER_TRIGGERS
                ORDER BY TABLE_NAME, TRIGGER_NAME";

            DataTable dt = _queryExecutor.ExecuteQuery(query);
            List<TriggerInfo> triggers = new List<TriggerInfo>();

            foreach (DataRow row in dt.Rows)
            {
                triggers.Add(new TriggerInfo
                {
                    TriggerName = row["TRIGGER_NAME"].ToString(),
                    TableName = row["TABLE_NAME"].ToString(),
                    Status = row["STATUS"].ToString(),
                    TriggerType = row["TRIGGER_TYPE"].ToString(),
                    TriggeringEvent = row["TRIGGERING_EVENT"].ToString()
                });
            }

            return triggers;
        }

        /// <summary>
        /// Obtém lista de todas as constraints do usuário
        /// </summary>
        public List<ConstraintInfo> GetAllConstraints()
        {
            string query = @"
                SELECT 
                    CONSTRAINT_NAME, 
                    TABLE_NAME, 
                    CONSTRAINT_TYPE, 
                    STATUS,
                    SEARCH_CONDITION,
                    R_CONSTRAINT_NAME
                FROM USER_CONSTRAINTS
                WHERE CONSTRAINT_TYPE IN ('P', 'R', 'U', 'C')
                ORDER BY TABLE_NAME, CONSTRAINT_NAME";

            DataTable dt = _queryExecutor.ExecuteQuery(query);
            List<ConstraintInfo> constraints = new List<ConstraintInfo>();

            foreach (DataRow row in dt.Rows)
            {
                string constraintType = row["CONSTRAINT_TYPE"].ToString();
                string constraintTypeDesc = GetConstraintTypeDescription(constraintType);

                constraints.Add(new ConstraintInfo
                {
                    ConstraintName = row["CONSTRAINT_NAME"].ToString(),
                    TableName = row["TABLE_NAME"].ToString(),
                    ConstraintType = constraintType,
                    ConstraintTypeDescription = constraintTypeDesc,
                    Status = row["STATUS"].ToString(),
                    SearchCondition = row["SEARCH_CONDITION"] != DBNull.Value ? row["SEARCH_CONDITION"].ToString() : null,
                    RConstraintName = row["R_CONSTRAINT_NAME"] != DBNull.Value ? row["R_CONSTRAINT_NAME"].ToString() : null
                });
            }

            return constraints;
        }

        /// <summary>
        /// Obtém lista de todas as tabelas do usuário
        /// </summary>
        public List<TableInfo> GetAllTables(string text = "", List<TableInfo> tablesGroup = null)
        {
            string query = $@"
SELECT 
    t.TABLE_NAME,
    t.NUM_ROWS,
    t.TABLESPACE_NAME
FROM USER_TABLES t
{text}
";

            DataTable dt = _queryExecutor.ExecuteQuery(query);
            List<TableInfo> tables = new List<TableInfo>();

            foreach (DataRow row in dt.Rows)
            {
                tables.Add(new TableInfo
                {
                    TableName = row["TABLE_NAME"].ToString(),
                    NumRows = row["NUM_ROWS"] != DBNull.Value ? Convert.ToInt64(row["NUM_ROWS"]) : 0,
                    MinMax = tablesGroup?.Find(t => t.TableName == row["TABLE_NAME"].ToString())?.MinMax,
                    Where = tablesGroup?.Find(t => t.TableName == row["TABLE_NAME"].ToString())?.Where,
                    TablespaceName = row["TABLESPACE_NAME"] != DBNull.Value ? row["TABLESPACE_NAME"].ToString() : null
                });
            }

            return tables;
        }

        /// <summary>
        /// Obtém o DDL de uma tabela
        /// </summary>
        public string GetTableDDL(string tableName)
        {
            try
            {
                string query = $"SELECT DBMS_METADATA.GET_DDL('TABLE', '{tableName}') FROM DUAL";
                object result = _queryExecutor.ExecuteScalar(query);
                return result != null ? result.ToString() : string.Empty;
            }
            catch (Exception ex)
            {
                return $"-- Erro ao obter DDL: {ex.Message}";
            }
        }

        /// <summary>
        /// Obtém o DML (INSERT statements) de uma tabela com suporte a CLOB e LGPD
        /// </summary>
        public List<string> GetTableDML(string tableName, string whereStat, string minMax, ref Int64 minId, ref Int64 maxId, ref Int64 minAutorizacao, ref Int64 maxAutorizacao)
        {
            List<string> dmlStatements = new List<string>();

            // 1. Obter colunas da tabela
            string columnsQuery = $"SELECT COLUMN_NAME FROM USER_TAB_COLUMNS WHERE TABLE_NAME = '{tableName}' ORDER BY COLUMN_ID";
            DataTable columnsDt = _queryExecutor.ExecuteQuery(columnsQuery);
            List<string> columnNames = new List<string>();
            foreach (DataRow row in columnsDt.Rows)
            {
                columnNames.Add(row["COLUMN_NAME"].ToString());
            }

            if (columnNames.Count == 0)
            {
                return dmlStatements; // Tabela sem colunas
            }

            string columnsList = string.Join(", ", columnNames);

            // 2. Obter dados da tabela
            string dataQuery = $"SELECT {columnsList} FROM {tableName} {whereStat}";
            DataTable dataDt = _queryExecutor.ExecuteQuery(dataQuery);

            // 3. Gerar comandos INSERT
            foreach (DataRow row in dataDt.Rows)
            {
                List<string> values = new List<string>();
                for (int i = 0; i < columnNames.Count; i++)
                {
                    object value = row[i];
                    string colName = columnNames[i]; // Nome da coluna para verificação LGPD e MinMax

                    // --- LÓGICA DE CONTROLE (ID, MIN, MAX) ---
                    if (value != DBNull.Value && IsNumericType(value.GetType()))
                    {
                        if (minMax != null && colName.Equals(minMax))
                        {
                            try
                            {
                                Int64 valor = Convert.ToInt64(value);
                                if (valor < minId) minId = valor;
                                if (valor > maxId) maxId = valor;
                                if (valor < minAutorizacao) minAutorizacao = valor;
                                if (valor > maxAutorizacao) maxAutorizacao = valor;
                            }
                            catch { }
                        }
                    }

                    // --- FORMATAÇÃO DO VALOR PARA SQL (COM LGPD + CLOB) ---
                    // Passamos tableName e colName para verificar se precisa ofuscar
                    string sqlValue = FormatOracleValue(value, tableName, colName);
                    values.Add(sqlValue);
                }

                string valuesList = string.Join(", ", values);
                dmlStatements.Add($"INSERT INTO {tableName} ({columnsList})\nVALUES ({valuesList});");
            }

            return dmlStatements;
        }

        /// <summary>
        /// Formata valores para SQL, aplicando LGPD e tratando CLOBs
        /// </summary>
        private string FormatOracleValue(object value, string tableName, string columnName)
        {
            // 1. VERIFICAÇÃO LGPD (OFUSCAMENTO)
            // Se a tabela e a coluna estiverem no dicionário de sensíveis, retorna valor mascarado
            if (_sensitiveColumns.TryGetValue(tableName.ToUpper(), out HashSet<string> sensitiveCols))
            {
                if (sensitiveCols.Contains(columnName.ToUpper()))
                {
                    return "'LGPD_PROTECTED'";
                }
            }

            // 2. TRATAMENTO DE NULOS
            if (value == null || value == DBNull.Value)
                return "NULL";

            Type type = value.GetType();

            // 3. NÚMEROS
            if (IsNumericType(type))
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture).Replace(",", ".");
            }

            // 4. DATAS
            if (value is DateTime dateTimeValue)
            {
                return $"TO_DATE('{dateTimeValue:dd/MM/yyyy HH:mm:ss}', 'DD/MM/YYYY HH24:MI:SS')";
            }

            // 5. STRINGS E CLOBS
            string strValue = value.ToString();
            strValue = strValue.Replace("'", "''"); // Escapa aspas simples

            // Limite seguro para literais (usando 2000 por segurança de encoding)
            if (strValue.Length <= 2000)
            {
                return $"'{strValue}'";
            }
            else
            {
                // --- ESTRATÉGIA DE CHUNKING PARA CLOB ---
                // Gera: TO_CLOB('parte1') || TO_CLOB('parte2') ...
                StringBuilder clobBuilder = new StringBuilder();
                int chunkSize = 2000;
                int length = strValue.Length;

                for (int i = 0; i < length; i += chunkSize)
                {
                    if (i > 0) clobBuilder.Append(" || ");

                    int currentChunkSize = Math.Min(chunkSize, length - i);
                    string chunk = strValue.Substring(i, currentChunkSize);

                    clobBuilder.Append($"TO_CLOB('{chunk}')");
                }

                return clobBuilder.ToString();
            }
        }

        private bool IsNumericType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        public string GetConstraintDDL(string constraintName)
        {
            try
            {
                string query = $"SELECT DBMS_METADATA.GET_DDL('CONSTRAINT', '{constraintName}') FROM DUAL";
                object result = _queryExecutor.ExecuteScalar(query);
                return result != null ? result.ToString() : string.Empty;
            }
            catch (Exception ex)
            {
                return $"-- Erro ao obter DDL: {ex.Message}";
            }
        }

        private string GetConstraintTypeDescription(string type)
        {
            switch (type)
            {
                case "P": return "Primary Key";
                case "R": return "Foreign Key";
                case "U": return "Unique";
                case "C": return "Check";
                default: return type;
            }
        }
    }
}