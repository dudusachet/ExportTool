using System;

namespace PLSQLExportFull.Models
{
    /// <summary>
    /// Representa informações de uma tabela do banco de dados
    /// </summary>
    public class TableInfo
    {
        /// <summary>
        /// Nome da tabela
        /// </summary>
        public string TableName { get; set; }
        public string Where { get; set; }
        public string MinMax { get; set; }

        /// <summary>
        /// Número de linhas (aproximado)
        /// </summary>
        public long NumRows { get; set; }

        /// <summary>
        /// Coluna para ordenação (Nome ou Linhas)
        /// </summary>
        public string SortColumn { get; set; } = "TableName";

        /// <summary>
        /// Direção da ordenação (Ascendente ou Descendente)
        /// </summary>
        public System.ComponentModel.ListSortDirection SortDirection { get; set; } = System.ComponentModel.ListSortDirection.Ascending;

        /// <summary>
        /// Nome do tablespace
        /// </summary>
        public string TablespaceName { get; set; }

        /// <summary>
        /// Retorna representação em string
        /// </summary>
        public override string ToString()
        {
            return $"{TableName} ({NumRows:N0} linhas)";
        }
    }
}
