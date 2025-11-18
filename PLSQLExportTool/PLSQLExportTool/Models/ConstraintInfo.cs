using System;

namespace PLSQLExportTool.Models
{
    /// <summary>
    /// Representa informações de uma constraint do banco de dados
    /// </summary>
    public class ConstraintInfo
    {
        /// <summary>
        /// Nome da constraint
        /// </summary>
        public string ConstraintName { get; set; }

        /// <summary>
        /// Nome da tabela associada
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// Tipo da constraint (P=PK, R=FK, U=Unique, C=Check)
        /// </summary>
        public string ConstraintType { get; set; }

        /// <summary>
        /// Descrição do tipo da constraint
        /// </summary>
        public string ConstraintTypeDescription { get; set; }

        /// <summary>
        /// Status da constraint (ENABLED/DISABLED)
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Condição de busca (para constraints CHECK)
        /// </summary>
        public string SearchCondition { get; set; }

        /// <summary>
        /// Nome da constraint referenciada (para Foreign Keys)
        /// </summary>
        public string RConstraintName { get; set; }

        /// <summary>
        /// Verifica se a constraint está habilitada
        /// </summary>
        public bool IsEnabled
        {
            get { return Status != null && Status.Equals("ENABLED", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Verifica se é uma Foreign Key
        /// </summary>
        public bool IsForeignKey
        {
            get { return ConstraintType == "R"; }
        }

        /// <summary>
        /// Verifica se é uma Primary Key
        /// </summary>
        public bool IsPrimaryKey
        {
            get { return ConstraintType == "P"; }
        }

        /// <summary>
        /// Retorna representação em string
        /// </summary>
        public override string ToString()
        {
            return $"{ConstraintName} ({TableName}) - {ConstraintTypeDescription} - {Status}";
        }
    }
}
