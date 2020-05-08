﻿/*
ChessLib, a chess data structure library

MIT License

Copyright (c) 2017-2020 Rudy Alex Kohn

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

namespace Rudz.Chess.Validation
{
    using Enums;
    using Extensions;
    using Hash;
    using System;
    using Types;

    public sealed class PositionValidator : IPositionValidator
    {
        private readonly IBoard _board;
        private readonly IPosition _pos;

        public PositionValidator(IPosition pos, IBoard board)
        {
            _pos = pos;
            _board = board;
        }

        public string ErrorMsg { get; private set; }
        public bool IsOk { get; private set; }

        public IPositionValidator Validate(PositionValidationTypes types = PositionValidationTypes.All)
        {
            var error = string.Empty;
            
            if (types.HasFlagFast(PositionValidationTypes.Basic))
                error = ValidateBasic();

            if (types.HasFlagFast(PositionValidationTypes.Castleling))
                error = ValidateCastleling(error);
            
            if (types.HasFlagFast(PositionValidationTypes.Kings))
                error = ValidateKings(error);
            
            if (types.HasFlagFast(PositionValidationTypes.Pawns))
                error = ValidatePawns(error);
            
            if (types.HasFlagFast(PositionValidationTypes.PieceConsistency))
                error = ValidatePieceConsistency(error);
            
            if (types.HasFlagFast(PositionValidationTypes.PieceCount))
                error = ValidatePieceCount(error);
            
            if (types.HasFlagFast(PositionValidationTypes.PieceTypes))
                error = ValidatePieceTypes(error);
            
            if (types.HasFlagFast(PositionValidationTypes.State))
                error = ValidateState(error);

            IsOk = error.IsNullOrEmpty();
            ErrorMsg = error;
            return this;
        }

        private string ValidateBasic()
        {
            var error = string.Empty;
            if (_pos.SideToMove != Player.White && _pos.SideToMove != Player.Black)
                error = AddError(error, $"{nameof(_pos.SideToMove)} is not a valid");

            if (_board.PieceAt(_pos.GetKingSquare(Player.White)) != Pieces.WhiteKing)
                error = AddError(error, $"white king position is not a white king");

            if (_board.PieceAt(_pos.GetKingSquare(Player.Black)) != Pieces.BlackKing)
                error = AddError(error, $"black king position is not a black king");

            if (_pos.EnPassantSquare != Square.None && _pos.EnPassantSquare.RelativeRank(_pos.SideToMove) != Ranks.Rank6)
                error = AddError(error, $"{nameof(_pos.EnPassantSquare)} square is not on rank 6");

            return error;
        }

        private string ValidateCastleling(string error)
        {
            Span<Player> players = stackalloc Player[] { Player.White, Player.Black };

            foreach (var c in players)
            {
                Span<CastlelingRights> crs = stackalloc CastlelingRights[] { CastlelingSides.King.MakeCastlelingRights(c), CastlelingSides.Queen.MakeCastlelingRights(c) };
                var ourRook = PieceTypes.Rook.MakePiece(c);
                foreach (var cr in crs)
                {
                    if (!_pos.CanCastle(cr))
                        continue;

                    var rookSq = _pos.CastlingRookSquare(cr);

                    if (_board.PieceAt(rookSq) != ourRook)
                        error = AddError(error, $"rook does not appear on its position for {c}");

                    if (_pos.GetCastlelingRightsMask(rookSq) != cr)
                        error = AddError(error, $"castleling rights mask at {rookSq} does not match for player {c}");

                    if ((_pos.GetCastlelingRightsMask(_pos.GetKingSquare(c).AsInt()) & cr) != cr)
                        error = AddError(error, $"castleling rights mask at {_pos.GetKingSquare(c)} does not match for player {c}");
                }
            }

            return error;
        }

        private string ValidateKings(string error)
        {
            Span<Player> players = stackalloc Player[] { Player.White, Player.Black };

            foreach (var player in players)
            {
                var count = _board.PieceCount(PieceTypes.King, player);
                if (count != 1)
                    error = AddError(error, $"king count for player {player} was {count}");
            }

            if (!(_pos.AttacksTo(_pos.GetKingSquare(~_pos.SideToMove)) & _board.Pieces(_pos.SideToMove)).IsEmpty)
                error = AddError(error, $"kings appear to attack each other");

            return error;
        }

        private string ValidatePawns(string error)
        {
            if (!(_board.Pieces(PieceTypes.Pawn) & (BitBoards.RANK1 | BitBoards.RANK8)).IsEmpty)
                error = AddError(error, $"pawns exists on rank 1 or rank 8");

            if (_board.PieceCount(PieceTypes.Pawn, Player.White) > 8)
                error = AddError(error, $"white side has more than 8 pawns");

            if (_board.PieceCount(PieceTypes.Pawn, Player.Black) > 8)
                error = AddError(error, $"black side has more than 8 pawns");

            return error;
        }

        private string ValidatePieceConsistency(string error)
        {
            if (!(_board.Pieces(Player.White) & _board.Pieces(Player.Black)).IsEmpty)
                error = AddError(error, $"white and black pieces overlap");

            if ((_board.Pieces(Player.White) | _board.Pieces(Player.Black)) != _board.Pieces())
                error = AddError(error, $"white and black pieces do not match all pieces");

            if (_board.Pieces(Player.White).Count > 16)
                error = AddError(error, $"white side has more than 16 pieces");

            if (_board.Pieces(Player.Black).Count > 16)
                error = AddError(error, $"black side has more than 16 pieces");

            return error;
        }

        private string ValidatePieceCount(string error)
        {
            Span<Piece> pieces = stackalloc Piece[] {
                Pieces.WhitePawn,
                Pieces.WhiteKnight,
                Pieces.WhiteBishop,
                Pieces.WhiteRook,
                Pieces.WhiteQueen,
                Pieces.WhiteKing,
                Pieces.BlackPawn,
                Pieces.BlackKnight,
                Pieces.BlackBishop,
                Pieces.BlackRook,
                Pieces.BlackQueen,
                Pieces.BlackKing
            };

            foreach (var pc in pieces)
            {
                var pt = pc.Type();
                var c = pc.ColorOf();
                if (_board.PieceCount(pt, c) != _board.Pieces(c, pt).Count)
                    error = AddError(error, $"piece count does not match for piece {pc}");

                // TODO : Validate piece list
            }

            return error;
        }

        private string ValidatePieceTypes(string error)
        {
            Span<PieceTypes> pts = stackalloc PieceTypes[]
            {
                PieceTypes.Pawn,
                PieceTypes.Knight,
                PieceTypes.Bishop,
                PieceTypes.Rook,
                PieceTypes.Queen,
                PieceTypes.King
            };

            foreach (var p1 in pts)
                foreach (var p2 in pts)
                {
                    if (p1 == p2 || (_board.Pieces(p1) & _board.Pieces(p2)).IsEmpty)
                        continue;

                    error = AddError(error, $"piece types {p1} and {p2} doesn't align");
                }

            return error;
        }

        private string ValidateState(string error)
        {
            var state = _pos.State;

            if (state == null)
            {
                error = AddError(error, "state is null");
                return error;
            }

            if (state.Key.Key == 0 && !_board.Pieces().IsEmpty)
                error = AddError(error, "state key is no valid");

            if (_board.Pieces(_pos.SideToMove, PieceTypes.Pawn).IsEmpty && state.PawnStructureKey.Key != Zobrist.ZobristNoPawn)
                error = AddError(error, "empty pawn key is invalid");

            if (state.Repetition < 0)
                error = AddError(error, $"{nameof(state.Repetition)} is negative");

            if (state.Rule50 < 0)
                error = AddError(error, $"{nameof(state.Rule50)} is negative");

            if (state.Equals(state.Previous))
                error = AddError(error, "state has itself as previous state");

            return error;
        }

        private static string AddError(string error, string message)
            => error.IsNullOrEmpty()
                ? message
                : $"{error}, {message}";
    }
}