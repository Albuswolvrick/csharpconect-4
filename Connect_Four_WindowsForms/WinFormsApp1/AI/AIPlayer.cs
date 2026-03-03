// ============================================================
// AIPlayer.cs – Computer opponent using Minimax + Alpha-Beta.
// Responsible for:
//   • Choosing the best column for the AI to play
//   • Scoring board positions heuristically
//   • Pruning the search tree efficiently (alpha-beta)
// ============================================================

using System;
using System.Collections.Generic;
using Connect4.GameLogic;

namespace Connect4.GameLogic
{
    /// <summary>
    /// Static class that implements the AI opponent.
    /// Uses Minimax with Alpha-Beta pruning to search ahead
    /// <see cref="SearchDepth"/> moves and pick the best column.
    /// </summary>
    public static class AIPlayer
    {
        // ── Configuration ────────────────────────────────────

        /// <summary>
        /// How many moves ahead the AI looks.
        /// Higher = stronger but slower. 5–7 is a good range for a 6×7 board.
        /// </summary>
        public static int SearchDepth { get; set; } = 5;

        // Scores used to evaluate terminal / near-terminal positions.
        private const int WIN_SCORE  =  100_000;
        private const int LOSE_SCORE = -100_000;

        // ── Public API ───────────────────────────────────────

        /// <summary>
        /// Returns the column index the AI should play next.
        /// Prefers the centre column on equal scores (statistically stronger).
        /// </summary>
        public static int ChooseMove(GameBoard board, CellState aiPlayer)
        {
            CellState human = (aiPlayer == CellState.Player1)
                ? CellState.Player2 : CellState.Player1;

            int bestScore = int.MinValue;
            int bestCol   = board.Cols / 2; // default to centre

            // Evaluate each column with alpha-beta minimax.
            foreach (int col in GetColumnOrder(board.Cols))
            {
                if (!board.IsColumnPlayable(col)) continue;

                int row = board.DropToken(col, aiPlayer);
                int score = Minimax(board, SearchDepth - 1, int.MinValue, int.MaxValue,
                                    false, aiPlayer, human, row, col);
                // Undo the move (set cell back to Empty).
                UndoMove(board, row, col);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCol   = col;
                }
            }

            return bestCol;
        }

        // ── Minimax with Alpha-Beta ───────────────────────────

        /// <summary>
        /// Recursively scores the board.
        /// <paramref name="isMaximising"/> = true when it is the AI's turn.
        /// <paramref name="lastRow"/> / <paramref name="lastCol"/> are the
        /// coordinates of the most-recently placed token (for fast win-check).
        /// </summary>
        private static int Minimax(GameBoard board, int depth,
                                    int alpha, int beta,
                                    bool isMaximising,
                                    CellState aiPlayer, CellState human,
                                    int lastRow, int lastCol)
        {
            // ── Terminal conditions ──────────────────────────
            CellState lastMover = isMaximising ? human : aiPlayer; // who just moved

            if (board.CheckWin(lastRow, lastCol))
                // Prefer winning sooner → add depth as a tie-breaker.
                return (lastMover == aiPlayer) ? WIN_SCORE + depth : LOSE_SCORE - depth;

            if (board.IsFull() || depth == 0)
                return EvaluateBoard(board, aiPlayer, human);

            // ── Recurse ──────────────────────────────────────
            if (isMaximising)
            {
                int maxScore = int.MinValue;
                foreach (int col in GetColumnOrder(board.Cols))
                {
                    if (!board.IsColumnPlayable(col)) continue;
                    int row = board.DropToken(col, aiPlayer);
                    int score = Minimax(board, depth - 1, alpha, beta,
                                        false, aiPlayer, human, row, col);
                    UndoMove(board, row, col);

                    maxScore = Math.Max(maxScore, score);
                    alpha    = Math.Max(alpha, score);
                    if (beta <= alpha) break; // ← Beta cut-off
                }
                return maxScore;
            }
            else
            {
                int minScore = int.MaxValue;
                foreach (int col in GetColumnOrder(board.Cols))
                {
                    if (!board.IsColumnPlayable(col)) continue;
                    int row = board.DropToken(col, human);
                    int score = Minimax(board, depth - 1, alpha, beta,
                                        true, aiPlayer, human, row, col);
                    UndoMove(board, row, col);

                    minScore = Math.Min(minScore, score);
                    beta     = Math.Min(beta, score);
                    if (beta <= alpha) break; // ← Alpha cut-off
                }
                return minScore;
            }
        }

        // ── Heuristic board evaluation ────────────────────────

        /// <summary>
        /// Scores the board for the AI when no terminal state is reached.
        /// Counts "windows" (consecutive runs of WinLength cells) and
        /// rewards partial fills by the AI, penalises them for the human.
        /// </summary>
        private static int EvaluateBoard(GameBoard board,
                                          CellState aiPlayer, CellState human)
        {
            int score = 0;
            int wl    = board.WinLength;

            // Bonus for occupying the centre column (strong positionally).
            int centreCol = board.Cols / 2;
            for (int r = 0; r < board.Rows; r++)
                if (board.GetCell(r, centreCol) == aiPlayer) score += 3;

            // Score every possible window of length WinLength.
            int[,] dirs = { { 0, 1 }, { 1, 0 }, { 1, 1 }, { 1, -1 } };

            for (int r = 0; r < board.Rows; r++)
            for (int c = 0; c < board.Cols; c++)
            for (int d = 0; d < 4; d++)
            {
                int dr = dirs[d, 0], dc = dirs[d, 1];

                // Gather a window of cells in this direction.
                var window = new List<CellState>(wl);
                for (int i = 0; i < wl; i++)
                {
                    int nr = r + dr * i, nc = c + dc * i;
                    if (nr < 0 || nr >= board.Rows || nc < 0 || nc >= board.Cols)
                        goto nextWindow; // window goes out of bounds
                    window.Add(board.GetCell(nr, nc));
                }
                score += ScoreWindow(window, aiPlayer, human);
                nextWindow:;
            }

            return score;
        }

        /// <summary>
        /// Returns a heuristic score for a single window of cells.
        /// </summary>
        private static int ScoreWindow(List<CellState> window,
                                        CellState ai, CellState human)
        {
            int aiCount    = 0, humanCount = 0, emptyCount = 0;
            foreach (var cell in window)
            {
                if      (cell == ai)    aiCount++;
                else if (cell == human) humanCount++;
                else                    emptyCount++;
            }

            // A window already mixed with both players is worthless.
            if (aiCount > 0 && humanCount > 0) return 0;

            int wl = window.Count;
            if (aiCount == wl)    return  WIN_SCORE;  // AI wins here (shouldn't reach eval)
            if (humanCount == wl) return  LOSE_SCORE; // human wins (same)

            // Reward partial AI fills; punish partial human fills.
            if (aiCount == wl - 1 && emptyCount == 1) return  100;
            if (aiCount == wl - 2 && emptyCount == 2) return   10;
            if (humanCount == wl - 1 && emptyCount == 1) return -80;
            if (humanCount == wl - 2 && emptyCount == 2) return  -8;

            return 0;
        }

        // ── Utilities ────────────────────────────────────────

        /// <summary>
        /// Returns columns ordered centre-outward.
        /// This lets alpha-beta prune more aggressively because
        /// centre moves are usually best and examined first.
        /// </summary>
        private static IEnumerable<int> GetColumnOrder(int cols)
        {
            int centre = cols / 2;
            yield return centre;
            for (int offset = 1; offset <= centre; offset++)
            {
                if (centre - offset >= 0)   yield return centre - offset;
                if (centre + offset < cols) yield return centre + offset;
            }
        }

        /// <summary>
        /// Removes a token from (row, col) to reverse a temporary move.
        /// Uses reflection-free direct access via GameBoard.DropToken's
        /// returned row — we store the row ourselves and reset it here.
        /// NOTE: This relies on GameBoard exposing an UndoToken method (see below).
        /// </summary>
        private static void UndoMove(GameBoard board, int row, int col)
        {
            board.UndoToken(row, col);
        }
    }
}
