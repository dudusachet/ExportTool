using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PLSQLExportTool.Data;
using PLSQLExportTool.Business;
using PLSQLExportTool.Models;
using System.IO;
using System.Text.Json;

namespace PLSQLExportTool.Forms
{
    public partial class ExportForm : Form
    {
        private OracleConnectionManager _connectionManager;
        private OracleQueryExecutor _queryExecutor;
        private MetadataRepository _metadataRepository;
        private ExportManager _exportManager;
        private List<TableInfo> _allTables;
        private List<TableGroup> _tableGroups;
        private SortOrder _sortOrder = SortOrder.Ascending;
        private string _sortBy = "Name";

        public ExportForm()
        {
            InitializeComponent();
            // Inicialização das dependências
            _connectionManager = new OracleConnectionManager();
            _queryExecutor = new OracleQueryExecutor(_connectionManager);
            _metadataRepository = new MetadataRepository(_queryExecutor);
            _exportManager = new ExportManager(_queryExecutor, _metadataRepository);

            // Inicialização da UI
            UpdateConnectionStatus();
            UpdateConnectionString();
            LoadTableGroups();

#if DEBUG
            txtHost.Text = "172.25.100.205";
            txtPort.Text = "1521";
            txtServiceName.Text = "XE";
            txtUserId.Text = "r22sp15";
            txtPassword.Text = "r22sp15";
#endif
        }

        // ====================================================================
        // Conexão
        // ====================================================================

        private void UpdateConnectionString()
        {
            string host = txtHost.Text;
            string port = txtPort.Text;
            string serviceName = txtServiceName.Text;
            string userId = txtUserId.Text;
            string password = txtPassword.Text;

            string connectionString = $"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT={port}))(CONNECT_DATA=(SERVICE_NAME={serviceName})));User Id={userId};Password={password};";
            txtConnectionString.Text = connectionString;
            _connectionManager.ConnectionString = connectionString;
        }

        private void UpdateConnectionStatus()
        {
            if (_connectionManager.IsConnected)
            {
                lblConnectionStatus.Text = "Status: Conectado";
                lblConnectionStatus.ForeColor = Color.Green;
            }
            else
            {
                lblConnectionStatus.Text = "Status: Desconectado";
                lblConnectionStatus.ForeColor = Color.Red;
                if (tabControl.SelectedTab != tabConnection)
                {
                    tabControl.SelectedTab = tabConnection;
                }
            }
        }

        private void txtConnectionField_TextChanged(object sender, EventArgs e)
        {
            UpdateConnectionString();
        }

        private void btnTestConnection_Click(object sender, EventArgs e)
        {
            try
            {
                toolStripStatusLabel.Text = "Testando conexão...";
                bool ok = _connectionManager.TestConnection();
                if (ok)
                {
                    MessageBox.Show("Conexão bem-sucedida!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    toolStripStatusLabel.Text = "Pronto";
                }
                else
                {
                    MessageBox.Show("Falha na conexão.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    toolStripStatusLabel.Text = "Falha na conexão";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha na conexão: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripStatusLabel.Text = "Falha na conexão";
            }
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                toolStripStatusLabel.Text = "Conectando...";
                _connectionManager.Connect();
                UpdateConnectionStatus();
                MessageBox.Show("Conectado com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                toolStripStatusLabel.Text = "Conectado";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Falha ao conectar: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripStatusLabel.Text = "Falha ao conectar";
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            _connectionManager.Disconnect();
            UpdateConnectionStatus();
            toolStripStatusLabel.Text = "Desconectado";
        }

        // ====================================================================
        // Exportação
        // ====================================================================

        private void LoadTableGroups()
        {
            try
            {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TableGroups.json");
                if (!File.Exists(jsonPath))
                {
                    MessageBox.Show("Arquivo TableGroups.json não encontrado. O agrupamento de tabelas não estará disponível.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string jsonString = File.ReadAllText(jsonPath);
                _tableGroups = JsonSerializer.Deserialize<List<TableGroup>>(jsonString);

                cmbTableGroups.Items.Clear();
                cmbTableGroups.Items.Add("Todos");
                foreach (var group in _tableGroups)
                {
                    cmbTableGroups.Items.Add(group.GroupName);
                }
                cmbTableGroups.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar grupos de tabelas: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnRefreshExportTables_Click(object sender, EventArgs e)
        {
            if (!_connectionManager.IsConnected)
            {
                MessageBox.Show("Por favor, conecte-se ao banco de dados primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
            }

            try
            {
                toolStripStatusLabel.Text = "Carregando tabelas...";
                var group = _tableGroups.FirstOrDefault(g => g.GroupName == cmbTableGroups.Text);
                
                _allTables = _metadataRepository.GetAllTables(group?.ToWhere(), group?.Tables); 
                
                SortAndDisplayTables(group == null);

                toolStripStatusLabel.Text = "Tabelas carregadas";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar tabelas: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripStatusLabel.Text = "Erro ao carregar tabelas";
            }
        }

        private void SortAndDisplayTables(bool ordenacao)
        {
            if (_allTables == null) return;

            // Ordenação


            if (_sortBy == "Name")
            {

                _allTables = _sortOrder == SortOrder.Ascending
                           ? _allTables
                            .OrderBy(t => t.MinMax == null ? 1 : 0) // P1: Garante que MinMax null (chave 1) vá para o final
                            .ThenBy(t => t.TableName)               // P2: Ordenação secundária por TableName
                            .ToList()
                          : _allTables
                            .OrderByDescending(t => t.TableName)    // Se for descendente, ordena apenas por TableName
                            .ToList();
            }
            else if (_sortBy == "Rows")
            {
                _allTables = _sortOrder == SortOrder.Ascending
                    ? _allTables.OrderBy(t => t.MinMax == null ? 1 : 0) // P1: Garante que MinMax null (chave 1) vá para o final
                                .ThenBy(t => t.NumRows).ToList()
                    : _allTables.OrderByDescending(t => t.NumRows).ToList();
            }

            // Preservar o estado de checagem
            var checkedTables = checkedListExportTables.CheckedItems.Cast<TableInfo>().Select(t => t.TableName).ToList();

            checkedListExportTables.Items.Clear();
            foreach (var table in _allTables)
            {
                checkedListExportTables.Items.Add(table, checkedTables.Contains(table.TableName));
                // Habilitar o controle para seleção
                checkedListExportTables.Enabled = true;
            }
        }

        private void btnSortByNameExport_Click(object sender, EventArgs e)
        {
            if (_sortBy == "Name")
            {
                _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                _sortBy = "Name";
                _sortOrder = SortOrder.Ascending;
            }
            SortAndDisplayTables(false);
        }

        private void btnSortByRowsExport_Click(object sender, EventArgs e)
        {
            if (_sortBy == "Rows")
            {
                _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            else
            {
                _sortBy = "Rows";
                _sortOrder = SortOrder.Ascending;
            }
            SortAndDisplayTables(false);
        }

        private void cmbTableGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            btnRefreshExportTables.PerformClick();

            // O usuário quer que ao selecionar um grupo, todas as tabelas desse grupo sejam marcadas.
            // O carregamento das tabelas já é feito em btnRefreshExportTables.PerformClick().
            
            if (_allTables == null || cmbTableGroups.SelectedIndex == -1) return;

            string selectedGroup = cmbTableGroups.SelectedItem.ToString();

            // Se o item selecionado for "Todos" (SelectedIndex == 0), habilita a seleção manual e o WHERE manual.
            if (selectedGroup == "Todos")
            {
                // Desmarca tudo para começar do zero
                for (int i = 0; i < checkedListExportTables.Items.Count; i++)
                {
                    checkedListExportTables.SetItemChecked(i, false);
                }
                checkedListExportTables.Enabled = true; // Habilita a seleção manual
                txtWhereClause.Enabled = true; // Habilita o WHERE manual
                return;
            }

            // Se um grupo específico foi selecionado
            var group = _tableGroups.FirstOrDefault(g => g.GroupName == selectedGroup);
            if (group == null) 
                return;

            // Marca todas as tabelas carregadas (que já são as do grupo, pois btnRefreshExportTables.PerformClick() filtrou)
            for (int i = 0; i < checkedListExportTables.Items.Count; i++)
            {
                checkedListExportTables.SetItemChecked(i, true);
            }
            
            checkedListExportTables.Enabled = true; // Habilita a seleção manual, mas o usuário pode desmarcar (novo requisito)
            txtWhereClause.Enabled = false; // Desabilita o WHERE manual, pois o WHERE do grupo será usado.
        }

        private void btnSelectAllExport_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListExportTables.Items.Count; i++)
            {
                checkedListExportTables.SetItemChecked(i, true);
            }
        }

        private void btnDeselectAllExport_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListExportTables.Items.Count; i++)
            {
                checkedListExportTables.SetItemChecked(i, false);
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            if (!_connectionManager.IsConnected)
            {
                MessageBox.Show("Por favor, conecte-se ao banco de dados primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedTables = checkedListExportTables.CheckedItems.Cast<TableInfo>().ToList();

            if (selectedTables.Count == 0)
            {
                MessageBox.Show("Selecione pelo menos uma tabela para exportar.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Scripts SQL (*.sql;*.pdc)|*.sql;*.pdc";
            saveFileDialog.Title = "Salvar Script DML de Exportação";
            saveFileDialog.FileName = cmbTableGroups.Text + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdc";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    toolStripStatusLabel.Text = "Exportando DML...";
                    // A cláusula WHERE agora é tratada individualmente por tabela no Select abaixo.
                    string manualWhere = txtWhereClause.Text.Trim();
                    bool isGroupSelected = cmbTableGroups.SelectedIndex > 0;

                    // Preparar lista conforme assinatura do ExportManager
                    var tablesToExport = selectedTables
                        .Select(t =>
                        {
                            string finalWhere = t.Where; // WHERE predefinido do TableGroup ou null

                            // Se NENHUM grupo está selecionado ("Todos" está selecionado) E há um WHERE manual,
                            // usa o WHERE manual para todas as tabelas selecionadas.
                            if (!isGroupSelected && !string.IsNullOrEmpty(manualWhere))
                            {
                                // Remove ponto e vírgula final, se houver, para evitar ORA-00933
                                finalWhere = manualWhere.TrimEnd(';');
                            }
                            // Se um grupo está selecionado, o WHERE predefinido (t.Where) é usado.
                            // Se "Todos" está selecionado e NÃO há WHERE manual, finalWhere permanece null (ou o valor de t.Where, que deve ser null neste caso).

                            return (t.TableName, finalWhere, t.MinMax);
                        })
                        .ToList();

                    // Chama o método que grava o arquivo diretamente
                    _exportManager.ExportTablesDML(tablesToExport, saveFileDialog.FileName);

                    MessageBox.Show($"Exportação DML concluída com sucesso!\nArquivo: {saveFileDialog.FileName}", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    toolStripStatusLabel.Text = "Exportação concluída";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro durante a exportação DML: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    toolStripStatusLabel.Text = "Erro na exportação";
                }
            }
        }
    }
}