using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PLSQLExportFull.Data;
using PLSQLExportFull.Business;
using PLSQLExportFull.Models;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace PLSQLExportFull.Forms
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
        private bool _isParsingConnectionString = false;

        public ExportForm()
        {
            InitializeComponent();

            this.MinimumSize = new System.Drawing.Size(620, 530);

            // Inicialização das dependências
            _connectionManager = new OracleConnectionManager();
            _queryExecutor = new OracleQueryExecutor(_connectionManager);
            _metadataRepository = new MetadataRepository(_queryExecutor);
            _exportManager = new ExportManager(_queryExecutor, _metadataRepository);

            this.StartPosition = FormStartPosition.CenterScreen;
            this.tabControl.Selecting += new TabControlCancelEventHandler(this.tabControl_Selecting);

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
            bool isConnected = _connectionManager.IsConnected;

            if (isConnected)
            {
                lblConnectionStatus.Text = "Status: Conectado";
                lblConnectionStatus.ForeColor = Color.Green;
            }
            else
            {
                lblConnectionStatus.Text = "Status: Desconectado";
                lblConnectionStatus.ForeColor = Color.Red;

                if (tabControl.SelectedTab == tabExport)
                {
                    tabControl.SelectedTab = tabConnection;
                }
            }
            bool enableInputs = !isConnected;

            // Caixas de Texto
            txtHost.Enabled = enableInputs;
            txtPort.Enabled = enableInputs;
            txtServiceName.Enabled = enableInputs;
            txtUserId.Enabled = enableInputs;
            txtPassword.Enabled = enableInputs;
            txtConnectionString.Enabled = enableInputs;

            // Botões de Configuração
            btnConnect.Enabled = enableInputs;       
            btnPasteString.Enabled = enableInputs;   
            btnLoadConfig.Enabled = enableInputs;    
            btnDisconnect.Enabled = isConnected;
        }

        private void txtConnectionField_TextChanged(object sender, EventArgs e)
        {
            if (_isParsingConnectionString) return;

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

        private void tabControl_Selecting(object sender, TabControlCancelEventArgs e)
        {
            // Se a aba que o usuário está TENTANDO abrir for a de Exportação...
            if (e.TabPage == tabExport)
            {
                // ... e NÃO estiver conectado
                if (!_connectionManager.IsConnected)
                {
                    // Cancela a troca de aba (o clique é ignorado)
                    e.Cancel = true;

                    // Opcional: Exibe um aviso sonoro ou mensagem
                    // System.Media.SystemSounds.Beep.Play(); 
                    MessageBox.Show("Conecte-se ao banco de dados para acessar esta aba.", "Acesso Negado", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
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

                // 1. LÓGICA DE SOBREPOSIÇÃO: Se já estiver conectado, desconecta primeiro
                if (_connectionManager.IsConnected)
                {
                    // Desconecta do banco anterior
                    _connectionManager.Disconnect();

                    // LIMPEZA DE DADOS: Importante para não exibir tabelas do banco antigo
                    checkedListExportTables.Items.Clear();
                    if (_allTables != null) _allTables.Clear();

                    // Opcional: reseta o combo de grupos
                    if (cmbTableGroups.Items.Count > 0) cmbTableGroups.SelectedIndex = 0;
                }

                // 2. Conecta no novo banco (com os dados atuais dos TextBoxes)
                _connectionManager.Connect();

                // 3. Atualiza a UI
                UpdateConnectionStatus();
                MessageBox.Show("Conectado com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                toolStripStatusLabel.Text = "Conectado";
            }
            catch (Exception ex)
            {
                // Se der erro ao conectar no novo, garante que o status fique como desconectado
                UpdateConnectionStatus();
                MessageBox.Show($"Falha ao conectar: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripStatusLabel.Text = "Falha ao conectar";
            }
        }
        // ====================================================================
        // CARREGAR ARQUIVO .CONFIG
        // ====================================================================
        private void btnLoadConfig_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Arquivos de Configuração (*.config)|*.config|Todos os Arquivos (*.*)|*.*";
            openFileDialog.Title = "Selecione o arquivo de configuração";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
                    doc.Load(openFileDialog.FileName);

                    var addNodes = doc.GetElementsByTagName("add");
                    string strConexao = "";

                    foreach (System.Xml.XmlNode node in addNodes)
                    {
                        if (node.Attributes["key"] != null && node.Attributes["key"].Value == "strConexaoBD")
                        {
                            if (node.Attributes["value"] != null)
                            {
                                strConexao = node.Attributes["value"].Value;
                                break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(strConexao))
                    {
                        // --- A CORREÇÃO ESTÁ AQUI ---

                        // 1. Levanta a trava para o TextChanged não sobrescrever o que estamos carregando
                        _isParsingConnectionString = true;

                        // 2. Joga o texto na tela (agora seguro)
                        txtConnectionString.Text = strConexao;

                        // 3. Abaixa a trava para que o parser possa funcionar
                        _isParsingConnectionString = false;

                        // 4. Chama o parser manualmente para ler a string e preencher Host, User, etc.
                        txtConnectionString_Leave(sender, e);

                        MessageBox.Show("Configuração importada com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Chave 'strConexaoBD' não encontrada.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    // Garante que a trava seja liberada em caso de erro
                    _isParsingConnectionString = false;
                    MessageBox.Show("Erro ao ler arquivo: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        // ====================================================================
        // PARSER AUTOMÁTICO: Quebra a string completa nos campos individuais
        // ====================================================================
        private void txtConnectionString_Leave(object sender, EventArgs e)
        {
            string fullString = txtConnectionString.Text.Trim();
            if (string.IsNullOrEmpty(fullString)) return;
            if (_isParsingConnectionString) return;

            try
            {
                _isParsingConnectionString = true; // Trava

                var builder = new Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder(fullString);

                // Preenche Usuário e Senha
                if (!string.IsNullOrEmpty(builder.UserID)) txtUserId.Text = builder.UserID;
                if (!string.IsNullOrEmpty(builder.Password)) txtPassword.Text = builder.Password;

                string dataSource = builder.DataSource;

                if (!string.IsNullOrEmpty(dataSource))
                {
                    // Regex melhorado para pegar HOST, PORT e SERVICE_NAME/SID
                    // Aceita qualquer caractere até fechar o parênteses ou encontrar espaço

                    var matchHost = Regex.Match(dataSource, @"HOST\s*=\s*([^)\s]+)", RegexOptions.IgnoreCase);
                    if (matchHost.Success) txtHost.Text = matchHost.Groups[1].Value;

                    var matchPort = Regex.Match(dataSource, @"PORT\s*=\s*(\d+)", RegexOptions.IgnoreCase);
                    if (matchPort.Success) txtPort.Text = matchPort.Groups[1].Value;

                    var matchService = Regex.Match(dataSource, @"SERVICE_NAME\s*=\s*([^)\s]+)", RegexOptions.IgnoreCase);
                    if (matchService.Success)
                    {
                        txtServiceName.Text = matchService.Groups[1].Value;
                    }
                    else
                    {
                        var matchSid = Regex.Match(dataSource, @"SID\s*=\s*([^)\s]+)", RegexOptions.IgnoreCase);
                        if (matchSid.Success) txtServiceName.Text = matchSid.Groups[1].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erro parser: " + ex.Message);
            }
            finally
            {
                _isParsingConnectionString = false; // Destrava
                                                    // Atualiza o gerenciador com a string final
                _connectionManager.ConnectionString = txtConnectionString.Text;
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
                cmbTableGroups.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar grupos de tabelas: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }




        private void RefreshTables()
        {
            // Verifica se está conectado
            if (!_connectionManager.IsConnected)
            {
                // Se quiser, pode remover o MessageBox para não ficar irritante
               // MessageBox.Show("Por favor, conecte-se ao banco de dados primeiro.", "Aviso");
                return;
            }

            if (cmbTableGroups.SelectedIndex == -1)
            {
                checkedListExportTables.Items.Clear();
                if (_allTables != null) _allTables.Clear();
                toolStripStatusLabel.Text = "Selecione um grupo para carregar as tabelas.";
                return;
            }

            try
            {
                this.Cursor = Cursors.WaitCursor; // Adiciona cursor de espera
                toolStripStatusLabel.Text = "Carregando tabelas...";

                // Pega o grupo selecionado
                var group = _tableGroups != null ? _tableGroups.FirstOrDefault(g => g.GroupName == cmbTableGroups.Text) : null;

                // Busca as tabelas no banco
                _allTables = _metadataRepository.GetAllTables(
                    group != null ? group.ToWhere() : null,
                    group != null ? group.Tables : null
                );

                // Atualiza a lista visual
                SortAndDisplayTables(group == null);

                toolStripStatusLabel.Text = "Tabelas carregadas";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar tabelas: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripStatusLabel.Text = "Erro ao carregar tabelas";
            }
            finally
            {
                this.Cursor = Cursors.Default; // Restaura o cursor
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
            // 1. Carrega os dados do banco (Chama o método acima)
            RefreshTables();

            // Validação de segurança
            if (_allTables == null || cmbTableGroups.SelectedIndex == -1) return;

            string selectedGroup = cmbTableGroups.SelectedItem.ToString();

            // 2. Lógica de Marcar/Desmarcar
            if (selectedGroup == "Todos")
            {
                // "Todos": Carrega lista, desmarca tudo e libera edição manual
                for (int i = 0; i < checkedListExportTables.Items.Count; i++)
                {
                    checkedListExportTables.SetItemChecked(i, false);
                }
                checkedListExportTables.Enabled = true;
                txtWhereClause.Enabled = true;
                return;
            }

            // Grupo Específico: Já veio filtrado do banco, apenas marcamos visualmente
            var group = _tableGroups.FirstOrDefault(g => g.GroupName == selectedGroup);
            if (group == null) return;

            for (int i = 0; i < checkedListExportTables.Items.Count; i++)
            {
                checkedListExportTables.SetItemChecked(i, true);
            }

            checkedListExportTables.Enabled = true;
            txtWhereClause.Enabled = false; // Trava WHERE manual
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
            public string GroupName { get; set; } 
            public bool Truncate { get; set; }
            public string Servidor { get; set; }
        }


        private void btnExport_Click(object sender, EventArgs e)
        {
            if (!_connectionManager.IsConnected)
            {
                MessageBox.Show("Conecte-se primeiro.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedTables = checkedListExportTables.CheckedItems.Cast<TableInfo>().ToList();
            if (selectedTables.Count == 0)
            {
                MessageBox.Show("Selecione tabelas.", "Aviso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "SQL (*.sql;*.pdc)|*.sql;*.pdc",
                FileName = $"{txtUserId.Text}_{cmbTableGroups.Text.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.sql.pdc"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                string manualWhere = txtWhereClause.Text.Trim();
                bool isGroup = cmbTableGroups.SelectedIndex > 0;

                var data = selectedTables.Select(t => new TableExportData
                {
                    TableName = t.TableName,
                    WhereClause = (!isGroup && !string.IsNullOrEmpty(manualWhere)) ? manualWhere.TrimEnd(';') : t.Where,
                    MinMax = t.MinMax
                }).ToList();


                ExportArguments args = new ExportArguments
                {
                    Tables = data,
                    FilePath = sfd.FileName,
                    GroupName = cmbTableGroups.Text,
                    Servidor = $"{txtUserId.Text}/ @{txtHost.Text}:{txtPort.Text}/{txtServiceName.Text}",
                    Truncate = chkTruncate.Checked
                };

                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += Worker_DoWork;
                worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

                this.Cursor = Cursors.WaitCursor;
                toolStripStatusLabel.Text = "Exportando DML... (Aguarde)";
                btnExport.Enabled = false;

                worker.RunWorkerAsync(args);
            }
        }


        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            ExportArguments args = (ExportArguments)e.Argument;


            _exportManager.ExportTablesDML(
                args.Tables,
                args.FilePath,
                args.GroupName,
                args.Truncate,
                args.Servidor
            );

            e.Result = args.FilePath;

            #region zipa arquivo
            var targetName = args.FilePath + ".7z";

            var p = new System.Diagnostics.ProcessStartInfo();
            p.FileName = Path.Combine(Application.StartupPath, "7za.exe");
            p.Arguments = "a -t7z -m0=lzma2 -mx=9 \"" + targetName + "\"";

            p.Arguments += " \"" + args.FilePath + "\"";

            //p.Arguments += " -sdel";

            p.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            var x = System.Diagnostics.Process.Start(p);
            x.WaitForExit();
            #endregion

        }

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