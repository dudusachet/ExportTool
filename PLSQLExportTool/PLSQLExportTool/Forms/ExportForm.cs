using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PLSQLExportTool.Data;
using PLSQLExportTool.Business;
using PLSQLExportTool.Models;
using System.IO;
using Newtonsoft.Json;

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
        // MÉTODOS DE CONEXÃO (RESTAURADOS)
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
        // EXPORTAÇÃO E CARREGAMENTO
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
                _tableGroups = JsonConvert.DeserializeObject<List<TableGroup>>(jsonString);

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

                _allTables = _metadataRepository.GetAllTables(group != null ? group.ToWhere() : null, group != null ? group.Tables : null);

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

            if (_sortBy == "Name")
            {
                _allTables = _sortOrder == SortOrder.Ascending
                           ? _allTables.OrderBy(t => t.MinMax == null ? 1 : 0).ThenBy(t => t.TableName).ToList()
                           : _allTables.OrderByDescending(t => t.TableName).ToList();
            }
            else if (_sortBy == "Rows")
            {
                _allTables = _sortOrder == SortOrder.Ascending
                    ? _allTables.OrderBy(t => t.MinMax == null ? 1 : 0).ThenBy(t => t.NumRows).ToList()
                    : _allTables.OrderByDescending(t => t.NumRows).ToList();
            }

            var checkedTables = checkedListExportTables.CheckedItems.Cast<TableInfo>().Select(t => t.TableName).ToList();

            checkedListExportTables.Items.Clear();
            foreach (var table in _allTables)
            {
                checkedListExportTables.Items.Add(table, checkedTables.Contains(table.TableName));
            }
            checkedListExportTables.Enabled = true;
        }

        // ====================================================================
        // BOTÕES AUXILIARES (RESTAURADOS)
        // ====================================================================

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

            if (_allTables == null || cmbTableGroups.SelectedIndex == -1) return;

            string selectedGroup = cmbTableGroups.SelectedItem.ToString();

            if (selectedGroup == "Todos")
            {
                for (int i = 0; i < checkedListExportTables.Items.Count; i++)
                {
                    checkedListExportTables.SetItemChecked(i, false);
                }
                checkedListExportTables.Enabled = true;
                txtWhereClause.Enabled = true;
                return;
            }

            var group = _tableGroups.FirstOrDefault(g => g.GroupName == selectedGroup);
            if (group == null) return;

            for (int i = 0; i < checkedListExportTables.Items.Count; i++)
            {
                checkedListExportTables.SetItemChecked(i, true);
            }

            checkedListExportTables.Enabled = true;
            txtWhereClause.Enabled = false;
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

        // ====================================================================
        // EXPORTAÇÃO FINAL (CORRIGIDA PARA .NET 4.0)
        // ====================================================================

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
            // CORREÇÃO DE FORMATO DO FILTRO
            saveFileDialog.Filter = "SQL Script (*.sql)|*.sql";
            saveFileDialog.Title = "Salvar Script DML de Exportação";

            // CORREÇÃO DE DATA NO NOME DO ARQUIVO
            string dataFormatada = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            saveFileDialog.FileName = $"{cmbTableGroups.Text}_{dataFormatada}.sql";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    toolStripStatusLabel.Text = "Exportando DML...";
                    string manualWhere = txtWhereClause.Text.Trim();
                    bool isGroupSelected = cmbTableGroups.SelectedIndex > 0;

                    // PREPARAÇÃO DOS DADOS USANDO CLASSE (SEM TUPLAS)
                    var tablesToExport = selectedTables
                        .Select(t =>
                        {
                            string finalWhere = t.Where;

                            if (!isGroupSelected && !string.IsNullOrEmpty(manualWhere))
                            {
                                finalWhere = manualWhere.TrimEnd(';');
                            }
                            TableExportData data = new TableExportData();
                            data.TableName = t.TableName;
                            data.WhereClause = finalWhere;
                            data.MinMax = t.MinMax;

                            return data;
                        })
                        .ToList();

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