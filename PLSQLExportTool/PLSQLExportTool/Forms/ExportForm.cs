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
using System.Text.RegularExpressions; // Necessário para o botão Colar String

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
        // MÉTODOS DE CONEXÃO
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

        // --- NOVA LÓGICA DO BOTÃO COLAR STRING ---
        private void btnPasteString_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                string textoCopiado = Clipboard.GetText();
                ImportarStringConexao(textoCopiado);
            }
        }

        private void ImportarStringConexao(string rawString)
        {
            // Regex para extrair: Usuario/Senha@//Host:Porta/Servico
            string pattern = @"^(?<user>[^/]+)/(?<pass>[^@]+)@(?://)?(?<host>[^:/]+):(?<port>\d+)/(?<service>.+)$";

            var match = Regex.Match(rawString.Trim(), pattern);

            if (match.Success)
            {
                txtUserId.Text = match.Groups["user"].Value;
                txtPassword.Text = match.Groups["pass"].Value;
                txtHost.Text = match.Groups["host"].Value;
                txtPort.Text = match.Groups["port"].Value;
                txtServiceName.Text = match.Groups["service"].Value;

                MessageBox.Show("Dados colados e preenchidos com sucesso!", "Importação", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("O formato da string na área de transferência não é válido.\n\nFormato esperado: Usuario/Senha@//Host:Porta/Servico", "Erro de Formato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        // ------------------------------------------

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
        // BOTÕES AUXILIARES
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
        // EXPORTAÇÃO FINAL
        // ====================================================================
        // Classe auxiliar para passar dados para o BackgroundWorker
        private class ExportArguments
        {
            public List<TableExportData> Tables { get; set; }
            public string FilePath { get; set; }
        }
        private void btnExport_Click(object sender, EventArgs e)
        {
            // --- Validações (Iguais ao seu código anterior) ---
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
            saveFileDialog.Filter = "SQL Script (*.sql;*.pdc)|*.sql;*.pdc";
            saveFileDialog.Title = "Salvar Script DML de Exportação";
            string dataFormatada = DateTime.Now.ToString("yyyyMMdd_HHmm");
            saveFileDialog.FileName = $"{cmbTableGroups.Text.Replace(" ", "_")}_{dataFormatada}.sql.pdc";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                // 1. Prepara os dados AQUI (Na Thread principal)
                // Isso é necessário porque o BackgroundWorker não pode ler txtWhereClause.Text diretamente
                string manualWhere = txtWhereClause.Text.Trim();
                bool isGroupSelected = cmbTableGroups.SelectedIndex > 0;

                var tablesToExport = selectedTables.Select(t =>
                {
                    string finalWhere = t.Where;
                    if (!isGroupSelected && !string.IsNullOrEmpty(manualWhere))
                    {
                        finalWhere = manualWhere.TrimEnd(';');
                    }
                    return new TableExportData
                    {
                        TableName = t.TableName,
                        WhereClause = finalWhere,
                        MinMax = t.MinMax
                    };
                }).ToList();

                // 2. Configura os argumentos
                ExportArguments args = new ExportArguments
                {
                    Tables = tablesToExport,
                    FilePath = saveFileDialog.FileName
                };

                // 3. Configura o BackgroundWorker
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += Worker_DoWork; // Onde o trabalho pesado acontece
                worker.RunWorkerCompleted += Worker_RunWorkerCompleted; // Onde termina

                // 4. Trava a UI visualmente
                this.Cursor = Cursors.WaitCursor; // Usa this.Cursor que é mais forte que Cursor.Current
                toolStripStatusLabel.Text = "Exportando DML... (Aguarde)";
                btnExport.Enabled = false; // Opcional: Evita duplo clique

                // 5. Inicia o processo
                worker.RunWorkerAsync(args);
            }
        }

        // Este método roda em outra Thread. NÃO MEXA EM CONTROLES DE TELA AQUI (MessageBox, TextBox, etc)
        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            // Recupera os argumentos passados
            ExportArguments args = (ExportArguments)e.Argument;

            // Chama seu ExportManager (Processo Pesado)
            // Se der erro aqui, o BackgroundWorker captura e passa para o Completed
            _exportManager.ExportTablesDML(args.Tables, args.FilePath);

            // Passamos o caminho do arquivo como resultado para usar na mensagem de sucesso
            e.Result = args.FilePath;
        }

        // Este método roda quando termina (Sucesso ou Erro) - Volta para a Thread da Tela
        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            // 1. Restaura o Cursor e a UI
            this.Cursor = Cursors.Default;
            btnExport.Enabled = true;

            // 2. Verifica se houve erro
            if (e.Error != null)
            {
                MessageBox.Show($"Erro durante a exportação DML: {e.Error.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripStatusLabel.Text = "Erro na exportação";
            }
            else
            {
                // Sucesso
                string filePath = (string)e.Result;
                MessageBox.Show($"Exportação DML concluída com sucesso!\nArquivo: {filePath}", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                toolStripStatusLabel.Text = "Exportação concluída";
            }
        }
    }
}