// ============================================================
// Form1.cs – Windows Forms UI layer.
// Responsible for:
//   • Drawing the grid (custom GDI+ painting)
//   • Translating mouse clicks to column plays
//   • Showing game-mode / grid-size controls
//   • Subscribing to GameController events to update the display
//   • NO game logic lives here – all decisions go through GameController
// ============================================================

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Connect4.GameLogic;

namespace Connect4.UI
{
    public partial class Form1 : Form
    {
        // ── Dependencies ─────────────────────────────────────
        private GameBoard      _board;
        private GameController _controller;

        // ── Drawing constants ────────────────────────────────
        private const int CELL_SIZE    = 80;  // pixels per grid cell
        private const int PADDING      = 20;  // board margin from form edge
        private const int TOKEN_MARGIN = 8;   // gap between token and cell edge

        // Colours
        private static readonly Color BOARD_COLOR   = Color.FromArgb(30, 100, 200);
        private static readonly Color EMPTY_COLOR   = Color.FromArgb(20, 20, 40);
        private static readonly Color PLAYER1_COLOR = Color.FromArgb(240, 60, 60);   // red
        private static readonly Color PLAYER2_COLOR = Color.FromArgb(240, 200, 30);  // yellow
        private static readonly Color HOVER_COLOR   = Color.FromArgb(80, 255, 255, 255); // translucent white

        // ── UI Controls (created in code – no Designer dependency) ──
        private Panel  _boardPanel;       // the painted grid lives here
        private Label  _statusLabel;      // shows whose turn / winner
        private Button _newGameBtn;
        private Button _pvpBtn;
        private Button _pvaiBtn;
        private Label  _rowsLabel, _colsLabel;
        private NumericUpDown _rowsSpinner, _colsSpinner;
        private Button _resizeBtn;

        // ── State ────────────────────────────────────────────
        private int _hoverCol = -1;          // column under the mouse cursor
        private GameMode _currentMode = GameMode.PlayerVsPlayer;

        // ── Constructor ──────────────────────────────────────
        public Form1()
        {
            Text            = "Connect Four";
            BackColor       = Color.FromArgb(15, 15, 30);
            ForeColor       = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            Font            = new Font("Segoe UI", 10f);

            // Create the default board (6 rows × 7 cols).
            _board      = new GameBoard(6, 7);
            _controller = new GameController(_board);

            // Subscribe to game-controller events so the UI reacts automatically.
            _controller.MoveMade  += OnMoveMade;
            _controller.BoardReset += OnBoardReset;

            BuildControls();
            ResizeFormToBoard();

            // Start the first game immediately.
            _controller.StartGame(_currentMode);
        }

        // ── Control construction ─────────────────────────────

        /// <summary>
        /// Builds all UI controls programmatically so the form
        /// works without a Designer file.
        /// </summary>
        private void BuildControls()
        {
            // ── Status label ─────────────────────────────────
            _statusLabel = new Label
            {
                AutoSize  = false,
                Height    = 36,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Dock      = DockStyle.None
            };

            // ── Game-mode buttons ─────────────────────────────
            _pvpBtn = MakeButton("👥 Player vs Player", Color.FromArgb(60, 120, 200));
            _pvpBtn.Click += (s, e) => { _currentMode = GameMode.PlayerVsPlayer; RestartGame(); };

            _pvaiBtn = MakeButton("🤖 Player vs AI", Color.FromArgb(120, 60, 200));
            _pvaiBtn.Click += (s, e) => { _currentMode = GameMode.PlayerVsAI; RestartGame(); };

            _newGameBtn = MakeButton("🔄 New Game", Color.FromArgb(40, 160, 80));
            _newGameBtn.Click += (s, e) => RestartGame();

            // ── Grid-size controls ────────────────────────────
            _rowsLabel   = MakeLabel("Rows:");
            _rowsSpinner = MakeSpinner(4, 10, _board.Rows);

            _colsLabel   = MakeLabel("Cols:");
            _colsSpinner = MakeSpinner(4, 12, _board.Cols);

            _resizeBtn = MakeButton("⊞ Apply Size", Color.FromArgb(180, 100, 30));
            _resizeBtn.Click += OnResizeClick;

            // ── Board panel ───────────────────────────────────
            _boardPanel = new Panel { BackColor = Color.Transparent };
            _boardPanel.Paint       += OnBoardPaint;
            _boardPanel.MouseMove   += OnBoardMouseMove;
            _boardPanel.MouseLeave  += (s, e) => { _hoverCol = -1; _boardPanel.Invalidate(); };
            _boardPanel.MouseClick  += OnBoardMouseClick;

            // ── Add all controls to form ──────────────────────
            Controls.AddRange(new Control[]
            {
                _boardPanel, _statusLabel,
                _pvpBtn, _pvaiBtn, _newGameBtn,
                _rowsLabel, _rowsSpinner,
                _colsLabel, _colsSpinner,
                _resizeBtn
            });
        }

        /// <summary>
        /// Positions every control and resizes the form to fit the current board.
        /// Called whenever the grid dimensions change.
        /// </summary>
        private void ResizeFormToBoard()
        {
            int boardW = _board.Cols * CELL_SIZE;
            int boardH = _board.Rows * CELL_SIZE;
            int sidebarX = PADDING + boardW + 20;
            int sidebarW = 180;

            // Position board panel.
            _boardPanel.SetBounds(PADDING, PADDING + 50, boardW, boardH);

            // Status label spans the board width.
            _statusLabel.SetBounds(PADDING, PADDING, boardW, 40);

            // Sidebar controls.
            int y = PADDING;
            void SidebarControl(Control c, int h)
            {
                c.SetBounds(sidebarX, y, sidebarW, h);
                y += h + 8;
            }

            SidebarControl(_pvpBtn,  36);
            SidebarControl(_pvaiBtn, 36);
            y += 8; // spacer
            SidebarControl(_newGameBtn, 36);
            y += 16; // spacer

            SidebarControl(_rowsLabel,   22);
            SidebarControl(_rowsSpinner, 28);
            SidebarControl(_colsLabel,   22);
            SidebarControl(_colsSpinner, 28);
            SidebarControl(_resizeBtn,   36);

            // Form size.
            ClientSize = new Size(
                sidebarX + sidebarW + PADDING,
                PADDING + 50 + boardH + PADDING
            );
        }

        // ── Event handlers: GameController → UI ──────────────

        /// <summary>
        /// Called by the controller after every move.
        /// Updates the status label and repaints the affected cell region.
        /// </summary>
        private void OnMoveMade(object sender, MoveEventArgs e)
        {
            // Invalidate just the cell that changed for efficiency.
            var cellRect = GetCellRect(e.Row, e.Col);
            _boardPanel.Invalidate(cellRect);

            // Update status text.
            switch (e.Result)
            {
                case MoveResult.Win:
                    string winner = e.Player == CellState.Player1 ? "🔴 Player 1" : "🟡 Player 2";
                    if (_currentMode == GameMode.PlayerVsAI && e.Player == CellState.Player2)
                        winner = "🤖 AI";
                    _statusLabel.Text      = $"{winner} wins! 🎉";
                    _statusLabel.ForeColor = e.Player == CellState.Player1 ? PLAYER1_COLOR : PLAYER2_COLOR;
                    break;

                case MoveResult.Draw:
                    _statusLabel.Text      = "It's a draw! 🤝";
                    _statusLabel.ForeColor = Color.Silver;
                    break;

                default:
                    UpdateTurnLabel();
                    break;
            }
        }

        /// <summary>
        /// Called by the controller when the board is fully reset.
        /// Forces a complete repaint.
        /// </summary>
        private void OnBoardReset(object sender, EventArgs e)
        {
            UpdateTurnLabel();
            _boardPanel.Invalidate();
        }

        // ── Event handlers: Board panel ───────────────────────

        /// <summary>
        /// Custom GDI+ painter for the Connect-Four grid.
        /// Draws the board background, then each cell/token.
        /// </summary>
        private void OnBoardPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int rows = _board.Rows;
            int cols = _board.Cols;

            // ── Board background ──────────────────────────────
            using (var brush = new SolidBrush(BOARD_COLOR))
                g.FillRectangle(brush, 0, 0, cols * CELL_SIZE, rows * CELL_SIZE);

            // ── Hover column highlight ────────────────────────
            if (_hoverCol >= 0 && _hoverCol < cols && !_controller.GameOver)
            {
                var hoverRect = new Rectangle(_hoverCol * CELL_SIZE, 0,
                                               CELL_SIZE, rows * CELL_SIZE);
                using (var brush = new SolidBrush(HOVER_COLOR))
                    g.FillRectangle(brush, hoverRect);
            }

            // ── Cells (tokens or empty holes) ─────────────────
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                CellState cell = _board.GetCell(r, c);
                Color tokenColor;

                switch (cell)
                {
                    case CellState.Player1: tokenColor = PLAYER1_COLOR; break;
                    case CellState.Player2: tokenColor = PLAYER2_COLOR; break;
                    default:                tokenColor = EMPTY_COLOR;    break;
                }

                Rectangle tokenRect = GetCellRect(r, c);
                tokenRect.Inflate(-TOKEN_MARGIN, -TOKEN_MARGIN);

                // Draw filled circle.
                using (var brush = new SolidBrush(tokenColor))
                    g.FillEllipse(brush, tokenRect);

                // Draw subtle border on filled tokens to make them pop.
                if (cell != CellState.Empty)
                {
                    using (var pen = new Pen(Color.FromArgb(60, Color.White), 2f))
                        g.DrawEllipse(pen, tokenRect);
                }
            }

            // ── Grid lines ────────────────────────────────────
            using (var pen = new Pen(Color.FromArgb(60, Color.Black), 1.5f))
            {
                for (int c = 1; c < cols; c++)
                    g.DrawLine(pen, c * CELL_SIZE, 0, c * CELL_SIZE, rows * CELL_SIZE);
                for (int r = 1; r < rows; r++)
                    g.DrawLine(pen, 0, r * CELL_SIZE, cols * CELL_SIZE, r * CELL_SIZE);
            }
        }

        /// <summary>
        /// Tracks the column under the mouse to show a hover highlight.
        /// </summary>
        private void OnBoardMouseMove(object sender, MouseEventArgs e)
        {
            int col = e.X / CELL_SIZE;
            if (col != _hoverCol)
            {
                _hoverCol = col;
                _boardPanel.Invalidate(); // repaint for hover effect
            }
        }

        /// <summary>
        /// Converts a mouse click position to a column and plays it.
        /// </summary>
        private void OnBoardMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            // In AI mode, only Player1 (human) clicks are accepted.
            if (_controller.GameOver) return;
            if (_controller.Mode == GameMode.PlayerVsAI
                && _controller.CurrentPlayer != CellState.Player1) return;

            int col = e.X / CELL_SIZE;
            _controller.PlayColumn(col);
        }

        // ── Grid resize ───────────────────────────────────────

        /// <summary>
        /// Reads the spinners, resizes the board, and starts a new game.
        /// </summary>
        private void OnResizeClick(object sender, EventArgs e)
        {
            int newRows = (int)_rowsSpinner.Value;
            int newCols = (int)_colsSpinner.Value;
            _board.Resize(newRows, newCols);    // only game logic is aware of the new size
            ResizeFormToBoard();                 // adapt the UI panels
            RestartGame();
        }

        // ── Helpers ───────────────────────────────────────────

        /// <summary>
        /// Returns the pixel rectangle on _boardPanel for cell (row, col).
        /// </summary>
        private Rectangle GetCellRect(int row, int col)
            => new Rectangle(col * CELL_SIZE, row * CELL_SIZE, CELL_SIZE, CELL_SIZE);

        private void RestartGame()
        {
            _controller.StartGame(_currentMode);
        }

        private void UpdateTurnLabel()
        {
            if (_controller.CurrentPlayer == CellState.Player1)
            {
                _statusLabel.Text      = "🔴 Player 1's turn";
                _statusLabel.ForeColor = PLAYER1_COLOR;
            }
            else
            {
                bool isAI = _currentMode == GameMode.PlayerVsAI;
                _statusLabel.Text      = isAI ? "🤖 AI is thinking…" : "🟡 Player 2's turn";
                _statusLabel.ForeColor = PLAYER2_COLOR;
            }
        }

        // ── Factory helpers for control creation ─────────────

        private Button MakeButton(string text, Color backColor)
        {
            return new Button
            {
                Text      = text,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                FlatAppearance = { BorderSize = 0 }
            };
        }

        private Label MakeLabel(string text)
        {
            return new Label
            {
                Text      = text,
                ForeColor = Color.LightGray,
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9.5f)
            };
        }

        private NumericUpDown MakeSpinner(int min, int max, int val)
        {
            return new NumericUpDown
            {
                Minimum   = min,
                Maximum   = max,
                Value     = val,
                BackColor = Color.FromArgb(35, 35, 55),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 10f)
            };
        }
    }
}
