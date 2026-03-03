// ============================================================
// GameController.cs – Orchestrates game flow.
// Responsible for:
//   • Tracking whose turn it is
//   • Calling the board to place tokens
//   • Evaluating MoveResult after each move
//   • Raising events that the UI subscribes to
//   • Supporting Player-vs-Player and Player-vs-AI modes
// ============================================================

using System;
using Connect4.GameLogic;

namespace Connect4.GameLogic
{
    /// <summary>
    /// Game modes supported by the controller.
    /// </summary>
    public enum GameMode { PlayerVsPlayer, PlayerVsAI }

    /// <summary>
    /// Event data sent after every move so the UI can redraw.
    /// </summary>
    public class MoveEventArgs : EventArgs
    {
        public int Row       { get; }
        public int Col       { get; }
        public CellState Player { get; }
        public MoveResult Result { get; }

        public MoveEventArgs(int row, int col, CellState player, MoveResult result)
        {
            Row = row; Col = col; Player = player; Result = result;
        }
    }

    /// <summary>
    /// Central controller: bridges UI input → board logic → UI output.
    /// The UI only calls <see cref="PlayColumn"/> and subscribes to events.
    /// </summary>
    public class GameController
    {
        // ── Dependencies ─────────────────────────────────────
        private readonly GameBoard _board;

        // ── State ────────────────────────────────────────────
        public CellState CurrentPlayer { get; private set; }
        public GameMode   Mode         { get; private set; }
        public bool       GameOver     { get; private set; }

        // ── Events ───────────────────────────────────────────
        /// <summary>Fired after every legal move (including AI moves).</summary>
        public event EventHandler<MoveEventArgs> MoveMade;

        /// <summary>Fired when the board needs a full repaint (new game / resize).</summary>
        public event EventHandler BoardReset;

        // ── Constructor ──────────────────────────────────────
        public GameController(GameBoard board)
        {
            _board = board;
        }

        // ── Public API ───────────────────────────────────────

        /// <summary>
        /// Starts a new game. Resets the board and picks Player1 to go first.
        /// </summary>
        public void StartGame(GameMode mode)
        {
            Mode = mode;
            GameOver = false;
            CurrentPlayer = CellState.Player1;
            _board.Reset();

            // Notify the UI to repaint the empty board.
            BoardReset?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Attempts to drop a token in <paramref name="col"/> for the current player.
        /// Returns false if the column is full or the game is already over.
        /// In AI mode this also triggers the AI's reply move automatically.
        /// </summary>
        public bool PlayColumn(int col)
        {
            if (GameOver) return false;
            if (!_board.IsColumnPlayable(col)) return false;

            // Place the human player's token.
            ExecuteMove(col, CurrentPlayer);

            // In AI mode, let the AI respond (unless the game just ended).
            if (!GameOver && Mode == GameMode.PlayerVsAI
                          && CurrentPlayer == CellState.Player2)
            {
                // The AI move is fired on the same thread – the UI event
                // handler will update the display for both moves.
                int aiCol = AIPlayer.ChooseMove(_board, CellState.Player2);
                ExecuteMove(aiCol, CellState.Player2);
            }

            return true;
        }

        // ── Private helpers ──────────────────────────────────

        /// <summary>
        /// Places a token, fires <see cref="MoveMade"/>, and switches turns.
        /// Sets <see cref="GameOver"/> on win or draw.
        /// </summary>
        private void ExecuteMove(int col, CellState player)
        {
            int row = _board.DropToken(col, player);
            if (row < 0) return; // column was full – should not happen here

            // Determine the result of this move.
            MoveResult result;
            if (_board.CheckWin(row, col))
            {
                result   = MoveResult.Win;
                GameOver = true;
            }
            else if (_board.IsFull())
            {
                result   = MoveResult.Draw;
                GameOver = true;
            }
            else
            {
                result = MoveResult.None;
                // Switch to the other player.
                CurrentPlayer = (player == CellState.Player1)
                    ? CellState.Player2
                    : CellState.Player1;
            }

            // Notify the UI.
            MoveMade?.Invoke(this, new MoveEventArgs(row, col, player, result));
        }
    }
}
