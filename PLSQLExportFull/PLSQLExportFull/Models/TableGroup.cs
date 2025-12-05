using System.Collections.Generic;

namespace PLSQLExportFull.Models
{
    public class TableGroup
    {
        public string GroupName { get; set; }
        public List<TableInfo> Tables { get; set; }

        public string ToWhere()
        {
            if (Tables == null || Tables.Count == 0) return "";
            List<string> names = new List<string>();
            foreach (var t in Tables) names.Add($"'{t.TableName}'");
            return $"WHERE TABLE_NAME IN ({string.Join(",", names)})";
        }
    }
}