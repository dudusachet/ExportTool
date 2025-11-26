using System;
using System.Data;
using Oracle.ManagedDataAccess.Client;

namespace PLSQLExportFull.Data
{
    /// <summary>
    /// Executa queries e comandos no banco de dados Oracle
    /// </summary>
    public class OracleQueryExecutor
    {
        private OracleConnectionManager _connectionManager;

        /// <summary>
        /// Construtor
        /// </summary>
        /// <param name="connectionManager">Gerenciador de conexões</param>
        public OracleQueryExecutor(OracleConnectionManager connectionManager)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        }

        /// <summary>
        /// Executa uma query SELECT e retorna um DataTable
        /// </summary>
        /// <param name="query">Query SQL</param>
        /// <returns>DataTable com os resultados</returns>
        public DataTable ExecuteQuery(string query)
        {
            if (!_connectionManager.IsConnected)
            {
                throw new InvalidOperationException("Não há conexão ativa com o banco de dados.");
            }

            DataTable dataTable = new DataTable();

            using (OracleCommand command = new OracleCommand(query, _connectionManager.Connection))
            {
                using (OracleDataAdapter adapter = new OracleDataAdapter(command))
                {
                    adapter.Fill(dataTable);
                }
            }

            return dataTable;
        }

        /// <summary>
        /// Executa um comando SQL (INSERT, UPDATE, DELETE, DDL)
        /// </summary>
        /// <param name="commandText">Comando SQL</param>
        /// <returns>Número de linhas afetadas (ou -1 para comandos DDL)</returns>
        public int ExecuteNonQuery(string commandText)
        {
            if (!_connectionManager.IsConnected)
            {
                throw new InvalidOperationException("Não há conexão ativa com o banco de dados.");
            }

            using (OracleCommand command = new OracleCommand(commandText, _connectionManager.Connection))
            {
                command.CommandType = CommandType.Text;
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executa um comando SQL e retorna um único valor
        /// </summary>
        /// <param name="commandText">Comando SQL</param>
        /// <returns>Valor retornado pela query</returns>
        public object ExecuteScalar(string commandText)
        {
            if (!_connectionManager.IsConnected)
            {
                throw new InvalidOperationException("Não há conexão ativa com o banco de dados.");
            }

            using (OracleCommand command = new OracleCommand(commandText, _connectionManager.Connection))
            {
                command.CommandType = CommandType.Text;
                return command.ExecuteScalar();
            }
        }

        /// <summary>
        /// Executa múltiplos comandos em uma transação
        /// </summary>
        /// <param name="commands">Array de comandos SQL</param>
        /// <returns>True se todos os comandos foram executados com sucesso</returns>
        public bool ExecuteTransaction(string[] commands)
        {
            if (!_connectionManager.IsConnected)
            {
                throw new InvalidOperationException("Não há conexão ativa com o banco de dados.");
            }

            OracleTransaction transaction = null;

            try
            {
                transaction = _connectionManager.Connection.BeginTransaction();

                foreach (string commandText in commands)
                {
                    using (OracleCommand command = new OracleCommand(commandText, _connectionManager.Connection))
                    {
                        command.Transaction = transaction;
                        command.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
                return true;
            }
            catch (Exception)
            {
                if (transaction != null)
                {
                    transaction.Rollback();
                }
                throw;
            }
        }
    }
}
