﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokemonBejeweled.Pokemon;

namespace PokemonBejeweled
{
    public delegate void BoardDirtiedEventHandler(object source);
    public delegate void PointsAddedEventHandler(object source);

    public class PokemonBoard
    {
        public static readonly int gridSize = 8;
        public event BoardDirtiedEventHandler BoardDirtied;
        public event PointsAddedEventHandler PointsAdded;
        private bool _undoAllowed = true;
        private Random _rand = new Random();
        public static Dictionary<int, Type> TokenDict = basicTokens();
        private PokemonGridHistory _pokemonHistory = new PokemonGridHistory();
        private int _pointsToAdd = 0;
        internal int PointsToAdd
        {
            get { return _pointsToAdd; }
        }
        private IBasicPokemonToken[,] _pokemonGrid = new IBasicPokemonToken[gridSize, gridSize];
        internal IBasicPokemonToken[,] PokemonGrid
        {
            get { return _pokemonGrid; }
            set { GridOperations.copyGrid(value, _pokemonGrid); }
        }
        private IBasicPokemonToken[,] _newPokemonGrid = new IBasicPokemonToken[gridSize, gridSize];
        internal IBasicPokemonToken[,] NewPokemonGrid
        {
            get { return _newPokemonGrid; }
            set { GridOperations.copyGrid(value, _newPokemonGrid); }
        }

        /// <summary>
        /// Constructs a grid of pokemon tokens. 
        /// </summary>
        public PokemonBoard()
        {
            generateGrid();
            _pokemonHistory.Clear();
            _pokemonHistory.Add((IBasicPokemonToken[,])_pokemonGrid.Clone());
        }

        /// <summary>
        /// Generates the inital grid of IBasicPokemonTokens for the board. 
        /// </summary>
        public virtual void generateGrid()
        {
            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    _pokemonGrid[row, col] = generateNewPokemon();
                }
            }
            updateBoard();
        }

        /// <summary>
        /// Generates a random basic pokemon. 
        /// </summary>
        /// <returns>A random basic pokemon of the type IBasicPokemonToken</returns>
        private IBasicPokemonToken generateNewPokemon()
        {
            int pokeNumber = _rand.Next(1, 8);
            return (IBasicPokemonToken)Activator.CreateInstance(TokenDict[pokeNumber]);
        }

        /// <summary>
        /// Checks to see if two locations on the grid are adjacent. 
        /// </summary>
        /// <param name="row1">The row of the first location on the grid. </param>
        /// <param name="col1">The column of the first location on the grid. </param>
        /// <param name="row2">The row of the second location on the grid. </param>
        /// <param name="col2">The column of the second location on the grid. </param>
        /// <returns>True if the two locations are adjacent, false otherwise. </returns>
        public virtual bool piecesAreAdjacent(int row1, int col1, int row2, int col2)
        {
            if (row1 == row2 && Math.Abs(col1 - col2) == 1)
            {
                return true;
            }
            if (col1 == col2 && Math.Abs(row1 - row2) == 1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Executes a play. 
        /// </summary>
        /// <param name="row1">The row of the first location on the grid. </param>
        /// <param name="col1">The column of the first location on the grid. </param>
        /// <param name="row2">The row of the second location on the grid. </param>
        /// <param name="col2">The column of the second location on the grid. </param>
        public virtual void makePlay(int row1, int col1, int row2, int col2)
        {
            bool madeMove = false;
            IBasicPokemonToken firstToken = _pokemonGrid[row1, col1];
            IBasicPokemonToken secondToken = _pokemonGrid[row2, col2];
            _pokemonGrid[row1, col1] = secondToken;
            _pokemonGrid[row2, col2] = firstToken;
            _newPokemonGrid[row1, col1] = secondToken;
            _newPokemonGrid[row2, col2] = firstToken;
            madeMove |= updateSingleRow(row1, col1);
            madeMove |= updateSingleRow(row2, col2);
            madeMove |= updateSingleColumn(row1, col1);
            madeMove |= updateSingleColumn(row2, col2);
            madeMove |= markDittoNulls(row1, col1, row2, col2);
            if (!madeMove)
            {
                _newPokemonGrid[row1, col1] = firstToken;
                _newPokemonGrid[row2, col2] = secondToken;
            }
            _pokemonGrid[row1, col1] = firstToken;
            _pokemonGrid[row2, col2] = secondToken;
        }

        /// <summary>
        /// Begins a play. 
        /// </summary>
        /// <param name="row1">The row of the first location on the grid. </param>
        /// <param name="col1">The column of the first location on the grid. </param>
        /// <param name="row2">The row of the second location on the grid. </param>
        /// <param name="col2">The column of the second location on the grid. </param>
        public virtual void startPlay(int row1, int col1, int row2, int col2)
        {
            _pointsToAdd = 0;
            if (piecesAreAdjacent(row1, col1, row2, col2))
            {
                makePlay(row1, col1, row2, col2);
                updateBoard();
                _pokemonHistory.Add((IBasicPokemonToken[,])_pokemonGrid.Clone());
                OnPointsAdded();
                _undoAllowed = true;
            }
        }

        /// <summary>
        /// Resets the board to the previous state. This can only be done once per turn. 
        /// </summary>
        public virtual void undoPlay()
        {
            if (_undoAllowed && 2 <= _pokemonHistory.PokemonHistory.Count)
            {
                GridOperations.copyGrid(_pokemonHistory.NextToLast(), _pokemonGrid);
                GridOperations.copyGrid(_pokemonHistory.NextToLast(), _newPokemonGrid);
                _pokemonHistory.RemoveAt(_pokemonHistory.PokemonHistory.Count - 1);
                _pointsToAdd = -_pointsToAdd;
                _undoAllowed = false;
                OnBoardDirtied();
            }
        }

        /// <summary>
        /// If the new grid state is different from the current grid state, new tokens are pulled down and the grid is updated. 
        /// </summary>
        public virtual void updateBoard()
        {
            while (!haveGridsStabilized())
            {
                pullDownTokens();
                updateAllRows();
                updateAllColumns();
            }
        }

        /// <summary>
        /// Checks to see if the new grid state is different from the current grid state. 
        /// </summary>
        /// <returns>True if the new grid state is different from the current grid state, false otherwise. </returns>
        private bool haveGridsStabilized()
        {
            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    if (null == _pokemonGrid[row, col] || null == _newPokemonGrid[row, col] || !_pokemonGrid[row, col].Equals(_newPokemonGrid[row, col]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Fills in empty spaces in the grid by first pulling down existing tokens and then adding new ones where necessary. 
        /// </summary>
        public virtual void pullDownTokens()
        {
            GridOperations.copyGrid(_newPokemonGrid, _pokemonGrid);
            OnBoardDirtied();
            int numberOfTokensToPullDown;
            for (int col = 0; col < gridSize; col++)
            {
                for (int row = gridSize - 1; row >= 0; row--)
                {
                    if (null == _pokemonGrid[row, col])
                    {
                        numberOfTokensToPullDown = 0;
                        while (row >= numberOfTokensToPullDown && null == _pokemonGrid[row - numberOfTokensToPullDown, col])
                        {
                            numberOfTokensToPullDown++;
                        }
                        if (row >= numberOfTokensToPullDown)
                        {
                            _pokemonGrid[row, col] = _pokemonGrid[row - numberOfTokensToPullDown, col];
                            _pokemonGrid[row - numberOfTokensToPullDown, col] = null;
                        }
                        else
                        {
                            while (numberOfTokensToPullDown > 0)
                            {
                                _pokemonGrid[--numberOfTokensToPullDown, col] = generateNewPokemon();
                            }
                        }
                    }
                }
            }
            GridOperations.copyGrid(_pokemonGrid, _newPokemonGrid);
            OnBoardDirtied();
        }

        /// <summary>
        /// Iterates through all the rows of the grid checking for rows of three or more and updates the new grid accordingly. 
        /// </summary>
        public virtual void updateAllRows()
        {
            int numberOfSameTokens;
            IBasicPokemonToken currentToken;
            for (int row = 0; row < gridSize; row++)
            {
                currentToken = _pokemonGrid[row, 0];
                numberOfSameTokens = 1;
                for (int col = 1; col < gridSize; col++)
                {
                    if (currentToken.isSameSpecies(_pokemonGrid[row, col]))
                    {
                        numberOfSameTokens++;
                    }
                    else if (3 <= numberOfSameTokens)
                    {
                        markNullRow(row, col - numberOfSameTokens, numberOfSameTokens);
                        evolveToken(row, col - numberOfSameTokens, numberOfSameTokens);
                        numberOfSameTokens = 1;
                    }
                    else
                    {
                        currentToken = _pokemonGrid[row, col];
                        numberOfSameTokens = 1;
                    }
                }
                if (3 <= numberOfSameTokens)
                {
                    markNullRow(row, gridSize - numberOfSameTokens, numberOfSameTokens);
                    evolveToken(row, gridSize - numberOfSameTokens, numberOfSameTokens);
                }
            }
        }

        /// <summary>
        /// Iterates through all the columns of the grid checking for columns of three or more and updates the new grid accordingly. 
        /// </summary>
        public virtual void updateAllColumns()
        {
            GridOperations.invertGrid(_pokemonGrid);
            GridOperations.invertGrid(_newPokemonGrid);
            updateAllRows();
            GridOperations.invertGrid(_pokemonGrid);
            GridOperations.invertGrid(_newPokemonGrid);
        }
        
        /// <summary>
        /// Checks to see if the given location is in a row of three or more. 
        /// </summary>
        /// <param name="rowStart">The row of the location to check. </param>
        /// <param name="colStart">The column of the location to check. </param>
        /// <returns>True if a row of three or more was found, false otherwise. </returns>
        public virtual bool updateSingleRow(int rowStart, int colStart)
        {
            IBasicPokemonToken startToken = _pokemonGrid[rowStart, colStart];
            int numberOfSameTokens = 1;

            int currentCol = colStart - 1;
            while (currentCol >= 0 && startToken.isSameSpecies(_pokemonGrid[rowStart, currentCol]))
            {
                numberOfSameTokens++;
                currentCol--;
            }
            currentCol = colStart + 1;
            while (currentCol < gridSize && startToken.isSameSpecies(_pokemonGrid[rowStart, currentCol]))
            {
                numberOfSameTokens++;
                currentCol++;
            }
            if (3 <= numberOfSameTokens)
            {
                markNullRow(rowStart, currentCol - numberOfSameTokens, numberOfSameTokens);
                evolveToken(rowStart, colStart, numberOfSameTokens);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks to see if the given location is in a column of three or more. 
        /// </summary>
        /// <param name="rowStart">The row of the location to check. </param>
        /// <param name="colStart">The column of the location to check. </param>
        /// <returns>True if a column of three or more was found, false otherwise. </returns>
        public virtual bool updateSingleColumn(int rowStart, int colStart)
        {
            GridOperations.invertGrid(_pokemonGrid);
            GridOperations.invertGrid(_newPokemonGrid);
            bool madeMove = updateSingleRow(colStart, rowStart);
            GridOperations.invertGrid(_pokemonGrid);
            GridOperations.invertGrid(_newPokemonGrid);
            return madeMove;
        }

        /// <summary>
        /// Checks to see if one of the two swapped locations is a DittoToken, and if so marks all
        /// tokens of the same type as was in the other location as null. 
        /// </summary>
        /// <param name="row1">The row of the first swapped location. </param>
        /// <param name="col1">The column of the first swapped location. </param>
        /// <param name="row2">The row of the second swapped location. </param>
        /// <param name="col2">The column of the second swapped location. </param>
        /// <returns>True if a DittoToken was swapped, false otherwise. </returns>
        public virtual bool markDittoNulls(int row1, int col1, int row2, int col2)
        {
            if (_pokemonGrid[row1, col1].GetType() == typeof(DittoToken))
            {
                markAllTokensOfSameTypeAsNull(_pokemonGrid[row2, col2]);
                _newPokemonGrid[row1, col1] = null;
                return true;
            }
            else if (_pokemonGrid[row2, col2].GetType() == typeof(DittoToken))
            {
                markAllTokensOfSameTypeAsNull(_pokemonGrid[row1, col1]);
                _newPokemonGrid[row2, col2] = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Marks the token at a given location null and adds 10 points to the play score. If the token is a
        /// first evolution pokemon, 30 additional points are added and the surrounding tokens are marked null. 
        /// If the token is a second evolution pokemon, 60 additional points are added and the tokens in the same
        /// row and column are marked null. 
        /// </summary>
        /// <param name="row">The row of the token to mark null. </param>
        /// <param name="col">The column of the token to mark null. </param>
        public virtual void updateToken(int row, int col)
        {
            if (_pokemonGrid[row, col].GetType().GetInterfaces().Contains(typeof(IFirstEvolutionPokemonToken)))
            {
                if (null != _newPokemonGrid[row, col])
                {
                    _pointsToAdd += 30;
                    _newPokemonGrid[row, col] = null;
                    markSurroundingTokensNull(row, col);
                }
            }
            else if (_pokemonGrid[row, col].GetType().GetInterfaces().Contains(typeof(ISecondEvolutionPokemonToken)))
            {
                if (null != _newPokemonGrid[row, col])
                {
                    _pointsToAdd += 60;
                    _newPokemonGrid[row, col] = null;
                    markFullRowAndColumnAsNull(row, col);
                }
            }
            _newPokemonGrid[row, col] = null;
            _pointsToAdd += 10;
        }

        /// <summary>
        /// Based on the number of same tokens in a row or column, evolves the token at the given 
        /// location. 4 of the same evolves to the first evolution and awards 100 bonus points, 
        /// 5 to Ditto with 300 bonus points, and 6 to the second evolution with 600 bonus points. 
        /// </summary>
        /// <param name="row">The row of the token to evolve. </param>
        /// <param name="col">The column of the token to evolve. </param>
        /// <param name="numberOfSameTokens">The number of same tokens in a row or column</param>
        public virtual void evolveToken(int row, int col, int numberOfSameTokens)
        {
            IBasicPokemonToken movedToken = _pokemonGrid[row, col];
            switch (numberOfSameTokens)
            {
                case 4:
                    _pointsToAdd += 100;
                    _newPokemonGrid[row, col] = movedToken.firstEvolvedToken();
                    break;
                case 5:
                    _pointsToAdd += 300;
                    _newPokemonGrid[row, col] = new DittoToken();
                    break;
                case 6:
                    _pointsToAdd += 600;
                    _newPokemonGrid[row, col] = movedToken.secondEvolvedToken();
                    break;
            }
        }

        /// <summary>
        /// Marks a row of tokens null based on the provided location and number tokens to mark null. 
        /// </summary>
        /// <param name="row">The row in which the row of three or more tokens existing. </param>
        /// <param name="colStart">The column at which the row of three or more tokens starts. </param>
        /// <param name="numberOfSameTokens">The number of same tokens in a row. </param>
        public virtual void markNullRow(int row, int colStart, int numberOfSameTokens)
        {
            if (3 <= numberOfSameTokens)
            {
                for (int i = 0; i < numberOfSameTokens; i++)
                {
                    updateToken(row, colStart + i);
                }
            }
        }
        
        /// <summary>
        /// Searches through the entire board and marks any tokens in the same evolution chain
        /// as the given IBasicPokemonToken as null. 
        /// </summary>
        /// <param name="pokemon">The pokemon for which all tokens of the same species will be marked null. </param>
        public virtual void markAllTokensOfSameTypeAsNull(IBasicPokemonToken pokemon)
        {
            int numTokensMarkedNull = 0;
            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    if (_pokemonGrid[row, col].isSameSpecies(pokemon))
                    {
                        numTokensMarkedNull++;
                        updateToken(row, col);
                    }
                }
            }
            _pointsToAdd += (int)Math.Pow(numTokensMarkedNull, 2) * 10;
        }

        /// <summary>
        /// Marks all tokens around a given location as null. 
        /// </summary>
        /// <param name="row">The row of the location around which to mark tokens as null. </param>
        /// <param name="col">The column of the location around which to mark tokens as null. </param>
        public virtual void markSurroundingTokensNull(int row, int col)
        {
            if (row - 1 >= 0)
            {
                updateToken(row - 1, col);
                if (col - 1 >= 0) updateToken(row - 1, col - 1); ;
                if (col + 1 < gridSize) updateToken(row - 1, col + 1); ;
            }
            if (col - 1 >= 0) updateToken(row, col - 1);
            if (col + 1 < gridSize) updateToken(row, col + 1);
            if (row + 1 < gridSize)
            {
                updateToken(row + 1, col);
                if (col - 1 >= 0) updateToken(row + 1, col - 1);
                if (col + 1 < gridSize) updateToken(row + 1, col + 1);
            }
        }

        /// <summary>
        /// Marks all tokens in the specified row and column as null. 
        /// </summary>
        /// <param name="row">The row to mark null. </param>
        /// <param name="col">The column to mark null. </param>
        public virtual void markFullRowAndColumnAsNull(int row, int col)
        {
            for (int currentRow = 0; currentRow < gridSize; currentRow++)
            {
                updateToken(currentRow, col);
            }
            for (int currentCol = 0; currentCol < gridSize; currentCol++)
            {
                updateToken(row, currentCol);
            }
        }

        /// <summary>
        /// Searches through the entire more to see if there are any moves left. If so, 
        /// returns true and the location of the move. 
        /// </summary>
        /// <param name="rowHint">The row of the token that can be switched to make a move. </param>
        /// <param name="colHint">The column of the token that can be switched to make a move. </param>
        /// <returns>True if a move is possible, false otherwise. </returns>
        public virtual bool areMovesLeft(out int rowHint, out int colHint)
        {
            bool isMove = false;
            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize - 1; col++)
                {
                    isMove = testForMove(row, col);
                    if (isMove)
                    {
                        rowHint = row;
                        colHint = col;
                        return true;
                    }
                    GridOperations.invertGrid(_pokemonGrid);
                    GridOperations.invertGrid(_newPokemonGrid);
                    isMove = testForMove(row, col);
                    GridOperations.invertGrid(_pokemonGrid);
                    GridOperations.invertGrid(_newPokemonGrid);
                    if (isMove)
                    {
                        rowHint = col;
                        colHint = row;
                        return true;
                    }
                }
            }
            rowHint = -1;
            colHint = -1;
            return false;
        }

        /// <summary>
        /// Checks to see if swapping the location with the token one to the right makes a valid move. 
        /// </summary>
        /// <param name="row">The row of the token to swap. </param>
        /// <param name="col">The column of the token to swap. </param>
        /// <returns>rue if a move is found, false otherwise. </returns>
        private bool testForMove(int row, int col)
        {
            int pointsFromLastPlay = _pointsToAdd;
            makePlay(row, col, row, col + 1);
            if (!haveGridsStabilized())
            {
                _pointsToAdd = -50;
                OnPointsAdded();
                _pointsToAdd = pointsFromLastPlay;
                GridOperations.copyGrid(_pokemonGrid, _newPokemonGrid);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Fired when the points for a play are finalized. 
        /// </summary>
        protected virtual void OnPointsAdded()
        {
            if (null != PointsAdded)
            {
                PointsAdded(this);
            }
        }

        /// <summary>
        /// Fired when the board changes. 
        /// </summary>
        protected virtual void OnBoardDirtied()
        {
            if (BoardDirtied != null)
            {
                BoardDirtied(this);
            }
        }

        /// <summary>
        /// A dictionary related integers to the seven types of basic tokens, used for
        /// randomly generating new tokens. 
        /// </summary>
        /// <returns>A dictionary mapping ints to pokemon types. </returns>
        private static Dictionary<int, Type> basicTokens()
        {
            Dictionary<int, Type> dict = new Dictionary<int, Type>();
            dict.Add(1, typeof(BulbasaurToken));
            dict.Add(2, typeof(CharmanderToken));
            dict.Add(3, typeof(ChikoritaToken));
            dict.Add(4, typeof(CyndaquilToken));
            dict.Add(5, typeof(PichuToken));
            dict.Add(6, typeof(SquirtleToken));
            dict.Add(7, typeof(TotodileToken));
            return dict;
        }
    }
}
