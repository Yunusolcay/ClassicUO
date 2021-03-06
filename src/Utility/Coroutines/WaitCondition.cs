﻿#region license
// Copyright (C) 2020 ClassicUO Development Community on Github
// 
// This project is an alternative client for the game Ultima Online.
// The goal of this is to develop a lightweight client considering
// new technologies.
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses/>.
#endregion

using System;

namespace ClassicUO.Utility.Coroutines
{
    public interface IWaitCondition
    {
        bool Update();
    }

    internal class WaitCondition : IWaitCondition
    {
        private readonly Func<bool> _condition;

        public WaitCondition(Func<bool> condition)
        {
            _condition = condition;
        }

        public bool Update()
        {
            return _condition();
        }
    }

    internal class WaitCondition<T> : IWaitCondition
    {
        private readonly Func<T, bool> _condition;
        private readonly T _parameter;

        public WaitCondition(Func<T, bool> condition, T startingValue)
        {
            _condition = condition;
            _parameter = startingValue;
        }

        public bool Update()
        {
            return _condition(_parameter);
        }
    }
}