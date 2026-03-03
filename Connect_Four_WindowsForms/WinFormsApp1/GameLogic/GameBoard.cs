// ============================================================
// GameBoard.cs – Pure game logic, NO UI dependencies.
// Responsible for:
//   • Storing the board state (2-D int array)
//   • Dropping tokens into columns
//   • Detecting wins / draws efficiently after every move
//   • Resizing the grid at runtime
// ============================================================

namespace Connect4.GameLogic
{
    /// <summary>
    /// Represents a single cell on the board.
    /// Empty = 0, Player1 = 1, Player2 = 2.
    /// </summary>
    public enum CellState { Empty = 0, Player1 = 1, Player2 = 2 }

    /// <summary>
    /// All possible outcomes after a move.
    /// </summary>
    public enum MoveResult { None, Win, Draw }

    /// <summary>
    /// Encapsulates the Connect-Four grid and all rules.
    /// The UI never touches the array directly – it only calls
    /// the public methods below.
    /// </summary>
    public class GameBoard
    {
        // ── Fields ──────────────────────────────────────────
        private CellState[,] _grid;   // [row, col], row 0 = top
        private int _rows;
        private int _cols;

        // How many tokens in a row are needed to win.
        public int WinLength { get; private set; }

        // Read-only grid access for the UI layer.
        public int Rows => _rows;
        public int Cols => _cols;

        // ── Constructor ──────────────────────────────────────
        /// <summary>
        /// Creates a board with the given dimensions.
        /// Default Connect-Four is 6 rows × 7 cols, win = 4.
        /// </summary>
        public GameBoard(int rows = 6, int cols = 7, int winLength = 4)
        {
            Resize(rows, cols, winLength);
        }

        // ── Public API ───────────────────────────────────────

        /// <summary>
        /// Returns the cell state at (row, col). Safe for UI use.
        /// </summary>
        public CellState GetCell(int row, int col) => _grid[row, col];

        /// <summary>
        /// Resizes the board and resets all cells to Empty.
        /// Call this when the player changes the grid size in the UI.
        /// </summary>
        public void Resize(int rows, int cols, int winLength = 4)
        {
            _rows = rows;
            _cols = cols;
            WinLength = winLength;
            _grid = new CellState[rows, cols];  // all Empty (= 0) by default
        }

        /// <summary>
        /// Resets all cells to Empty without changing the board size.
        /// </summary>
        public void Reset()
        {
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    _grid[r, c] = CellState.Empty;
        }

        /// <summary>
        /// Returns true if column <paramref name="col"/> still has room.
        /// </summary>
        public bool IsColumnPlayable(int col)
        {
            if (col < 0 || col >= _cols) return false;
            return _grid[0, col] == CellState.Empty; // top row empty → space left
        }

        /// <summary>
        /// Drops a token for <paramref name="player"/> into the given column.
        /// Returns the row the token landed on, or -1 if the column is full.
        /// </summary>
        public int DropToken(int col, CellState player)
        {
            if (!IsColumnPlayable(col)) return -1;

            // Gravity: scan from the bottom row upward.
            for (int row = _rows - 1; row >= 0; row--)
            {
                if (_grid[row, col] == CellState.Empty)
                {
                    _grid[row, col] = player;
                    return row;  // caller can use this for win-check optimisation
                }
            }
            return -1; // should never reach here after IsColumnPlayable check
        }

        /// <summary>
        /// Checks whether the board is completely full (draw condition).
        /// </summary>
        public bool IsFull()
        {
            for (int c = 0; c < _cols; c++)
                if (_grid[0, c] == CellState.Empty) return false;
            return true;
        }

        /// <summary>
        /// Efficient win check: only examines lines that pass through
        /// (lastRow, lastCol) – the cell just played.
        /// Returns true if that cell creates a winning sequence.
        /// </summary>
        public bool CheckWin(int lastRow, int lastCol)
        {
            CellState player = _grid[lastRow, lastCol];
            if (player == CellState.Empty) return false;

            // The four directions to check: horizontal, vertical,
            // diagonal (\) and anti-diagonal (/).
            int[,] directions = {
                { 0,  1 },   // horizontal →
                { 1,  0 },   // vertical   ↓
                { 1,  1 },   // diagonal   ↘
                { 1, -1 }    // anti-diag  ↙
            };

            for (int d = 0; d < 4; d++)
            {
                int dr = directions[d, 0];
                int dc = directions[d, 1];

                // Count consecutive tokens in both directions along this axis.
                int count = 1
                    + CountInDirection(lastRow, lastCol, dr, dc, player)
                    + CountInDirection(lastRow, lastCol, -dr, -dc, player);

                if (count >= WinLength) return true;
            }
            return false;
        }

        /// <summary>
        /// Removes a token from (row, col) – used only by the AI to undo
        /// temporary moves during the Minimax search tree traversal.
        /// Do NOT call this from UI code.
        /// </summary>
        public void UndoToken(int row, int col)
        {
            _grid[row, col] = CellState.Empty;
        }

        // ── Private helpers ──────────────────────────────────

        /// <summary>
        /// Counts how many consecutive cells owned by <paramref name="player"/>
        /// exist starting from (row+dr, col+dc) and continuing in (dr, dc).
        /// </summary>
        private int CountInDirection(int row, int col, int dr, int dc, CellState player)
        {
            int count = 0;
            int r = row + dr;
            int c = col + dc;

            // Walk as long as we stay in-bounds and hit the same player.
            while (r >= 0 && r < _rows && c >= 0 && c < _cols
                   && _grid[r, c] == player)
            {
                count++;
                r += dr;
                c += dc;
            }
            return count;
        }
    }
}
