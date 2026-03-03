# Connect Four – Refactored C# / WinForms

## Project Structure

```
Connect4/
├── GameLogic/
│   ├── GameBoard.cs        ← Pure board state & rules (NO UI)
│   └── GameController.cs   ← Turn management, event dispatcher
├── AI/
│   └── AIPlayer.cs         ← Minimax + Alpha-Beta opponent
├── UI/
│   └── Form1.cs            ← All drawing & mouse input (NO game logic)
├── Program.cs              ← Entry point
└── Connect4.csproj
```

## Architecture Layers

### 1. `GameLogic/GameBoard.cs`
- Owns the `CellState[,]` grid
- `DropToken(col, player)` → returns the row the token landed on
- `CheckWin(lastRow, lastCol)` → O(WinLength) efficient check on the last move only
- `Resize(rows, cols)` → runtime grid resizing
- `UndoToken(row, col)` → used by the AI to reverse temporary moves during search

### 2. `GameLogic/GameController.cs`
- Knows which player's turn it is
- Calls `GameBoard` to execute moves
- Fires `MoveMade` and `BoardReset` events → UI reacts without polling
- In `PlayerVsAI` mode, automatically calls `AIPlayer.ChooseMove` after each human move

### 3. `AI/AIPlayer.cs`
- **Minimax with Alpha-Beta pruning** – searches `SearchDepth` (default 5) moves ahead
- Columns evaluated centre-first for better pruning efficiency
- Heuristic `EvaluateBoard` scores partial windows (runs of 2/3) as well as centre occupancy
- Completely stateless – mutates the board temporarily and calls `UndoToken` to restore it

### 4. `UI/Form1.cs`
- Custom GDI+ painting on a `Panel` (no PictureBox or buttons per cell)
- Column hover highlight via `MouseMove`
- Mouse click → column index → `GameController.PlayColumn`
- `NumericUpDown` spinners + "Apply Size" button for runtime grid resizing
- Mode buttons (Player vs Player / Player vs AI) restart the game in the chosen mode
- **Zero game logic** – all decisions are delegated to `GameController`

## Building

```bash
dotnet build
dotnet run
```

Requires **.NET 6+ SDK** on Windows (WinForms is Windows-only).

## Features

| Feature | Where |
|---|---|
| Player vs Player | `GameController` + `Form1` mode buttons |
| Player vs AI (Minimax) | `AIPlayer.cs` |
| Efficient win detection | `GameBoard.CheckWin` (only checks through last move) |
| Adjustable grid size | `NumericUpDown` spinners + `GameBoard.Resize` |
| Clean separation of concerns | Logic / AI / UI in separate namespaces |
| Commented code | Every class & method has XML-doc comments |
