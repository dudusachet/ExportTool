using System.Collections.Generic;

namespace PLSQLExportTool.Models
{
    /// <summary>
    /// Representa um grupo lógico de tabelas (para seleção rápida na UI)
    /// </summary>
    public class TableGroup
    {
        public string GroupName { get; set; }
        public List<TableInfo> Tables { get; set; } = new List<TableInfo>();

        public string ToWhere()
        {
            if (Tables == null || Tables.Count == 0 || GroupName.Equals("Todos"))
                return string.Empty;

            return " WHERE TABLE_NAME IN ('" + string.Join("', '", Tables.ConvertAll(t => t.TableName)) + "' )";
        }
    }
}