using System;
using Oracle.ManagedDataAccess.Client;

namespace PLSQLExportTool.Data
{
    /// <summary>
    /// Gerencia conexões com o banco de dados Oracle
    /// </summary>
    public class OracleConnectionManager
    {
        private string _connectionString;
        private OracleConnection _connection;

        /// <summary>
        /// Obtém ou define a string de conexão
        /// </summary>
        public string ConnectionString
        {
            get { return _connectionString; }
            set { _connectionString = value; }
        }

        /// <summary>
        /// Obtém a conexão atual
        /// </summary>
        public OracleConnection Connection
        {
            get { return _connection; }
        }

        /// <summary>
        /// Verifica se está conectado
        /// </summary>
        public bool IsConnected
        {
            get { return _connection != null && _connection.State == System.Data.ConnectionState.Open; }
        }

        /// <summary>
        /// Construtor padrão
        /// </summary>
        public OracleConnectionManager()
        {
        }

        /// <summary>
        /// Construtor com string de conexão
        /// </summary>
        /// <param name="connectionString">String de conexão Oracle</param>
        public OracleConnectionManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Testa a conexão com o banco de dados
        /// </summary>
        /// <returns>True se a conexão foi bem-sucedida</returns>
        public bool TestConnection()
        {
            try
            {
                using (OracleConnection testConn = new OracleConnection(_connectionString))
                {
                    testConn.Open();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Abre uma conexão com o banco de dados
        /// </summary>
        public void Connect()
        {
            if (_connection != null && _connection.State == System.Data.ConnectionState.Open)
            {
                return;
            }

            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("String de conexão não foi definida.");
            }

            _connection = new OracleConnection(_connectionString);
            _connection.Open();
        }

        /// <summary>
        /// Fecha a conexão com o banco de dados
        /// </summary>
        public void Disconnect()
        {
            if (_connection != null)
            {
                if (_connection.State == System.Data.ConnectionState.Open)
                {
                    _connection.Close();
                }
                _connection.Dispose();
                _connection = null;
            }
        }

        /// <summary>
        /// Obtém uma nova conexão (para operações paralelas)
        /// </summary>
        /// <returns>Nova instância de OracleConnection</returns>
        public OracleConnection GetNewConnection()
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("String de conexão não foi definida.");
            }

            OracleConnection newConn = new OracleConnection(_connectionString);
            newConn.Open();
            return newConn;
        }

    }
}
