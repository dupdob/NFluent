﻿// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="CheckLogic.cs" company="NFluent">
//   Copyright 2018 Thomas PIERRAIN & Cyrille DUPUYDAUBY
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//       http://www.apache.org/licenses/LICENSE-2.0
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace NFluent.Kernel
{
    using System.Collections;
    using System.Collections.Generic;
    using Extensibility;
    using Extensions;

// the system namespace is not imported for older Net version. This allows to overload the definition of delegate types. 
#if !DOTNET_35 && !DOTNET_20 && !DOTNET_30
    using System;
#endif
//    [DebuggerNonUserCode]
    internal class CheckLogic<T> : ICheckLogic<T>
    {
        // TODO: Refactor
        private const string CanTBeUsedWhenNegated = "{0} can't be used when negated";

        private readonly FluentSut<T> fluentSut;
        private ICheckLogicBase child;
        private bool isRoot;
        private string comparison;
        private object expected;
        private long expectedCount;
        private ValueKind expectedKind = ValueKind.Value;
        private System.Type expectedType;
        private bool enforceExpectedType;
        private bool failed;
        private long index;
        private string label;
        private string lastError;

        private string negatedComparison;
        private string negatedError;
        private bool negatedFailed;
        private bool doNotNeedNegatedMessage;
        private bool cannotBetNegated;
        private string negatedLabel;
        private MessageOption negatedOption;
        private MessageOption options = MessageOption.None;
        private string sutName;

        private bool withExpected;
        private bool withGiven;

        public CheckLogic(FluentSut<T> fluentSut)
        {
            this.fluentSut = fluentSut;
            this.isRoot = true;
        }

        public bool IsNegated => this.fluentSut.Negated;

        public string LastError => this.IsNegated ? this.negatedError : this.lastError;

        public string Label => this.IsNegated ? this.negatedLabel : this.label;

        public MessageOption Option => this.IsNegated ? this.negatedOption : this.options;

        public string SutName => string.IsNullOrEmpty(this.fluentSut.SutName) ? this.sutName : this.fluentSut.SutName;

        public string Comparison => this.IsNegated ? this.negatedComparison : this.comparison;

        public bool Failed => this.failed || this.child != null && this.child.Failed;

        public  ICheckLogic<T> CantBeNegated(string checkName)
        {
            var message = string.Format(CanTBeUsedWhenNegated, checkName);
            this.SetNotNegatable(message);
            return this;
        }

        private void SetNotNegatable(string message)
        {
            this.DoNotNeedNegatedMessage();
            this.cannotBetNegated = true;
            if (this.IsNegated)
            {
                throw new System.InvalidOperationException(message);
            }
            this.OnNegateWhen(_ => true, message, MessageOption.NoCheckedBlock);
        }

        public ICheckLogic<T> FailIfNull(string error = "The {0} is null.")
        {
            if (this.fluentSut.Value != null)
            {
                return this;
            }

            this.Fail(error, MessageOption.NoCheckedBlock);
            this.DoNotNeedNegatedMessage();
            return this;
        }


        public ICheckLogic<T> DoNotNeedNegatedMessage()
        {
            this.doNotNeedNegatedMessage = true;
            return this;
        }

        public ICheckLogic<T> Analyze(Action<T, ICheckLogic<T>> action)
        {
            if (this.failed)
            {
                return this;
            }

            action(this.fluentSut.Value, this);
            return this;
        }

        public ICheckLogic<T> Fail(string error, MessageOption noCheckedBlock)
        {
            this.failed = true;
            if (this.IsNegated)
            {
                return this;
            }

            this.lastError = error;
            this.options = this.options | noCheckedBlock;

            return this;
        }

        public ICheckLogic<T> SetSutName(string name)
        {
            this.sutName = name;
            return this;
        }

        public ICheckLogic<TU> CheckSutAttributes<TU>(Func<T, TU> sutExtractor, string propertyName)
        {
            var value = this.fluentSut.Value;
            var sutWrapper = new FluentSut<TU>(value == null ? default(TU) : sutExtractor(value),
                this.fluentSut.Reporter,
                this.IsNegated) {CustomMessage = this.fluentSut.CustomMessage};

            var result =
                new CheckLogic<TU>(sutWrapper) {isRoot = false};

            if (this.cannotBetNegated)
            {
                result.SetNotNegatable(this.negatedError);
            }
            
            var finalSutName = string.IsNullOrEmpty(this.sutName) ? (this.fluentSut.SutName ?? "value") : this.sutName;
            if (!string.IsNullOrEmpty(propertyName))
            {
                result.SetSutName($"{finalSutName}'s {propertyName}");
            }
            if (this.failed != this.IsNegated)
            {
                result.failed = this.failed;
                result.negatedFailed = this.negatedFailed;
                result.lastError = this.lastError;
                result.negatedError = this.negatedError;
                result.negatedOption = this.negatedOption;
                result.options = this.options;
            }

            this.child = result;
            return result;
        }

        public void EndCheck()
        {
            this.child?.EndCheck();
            if (this.isRoot)
            {
                if (string.IsNullOrEmpty(this.negatedError) && !this.doNotNeedNegatedMessage)
                {
                    throw new System.InvalidOperationException("Negated error message was not specified. Use 'OnNegate' method to specify one.");
                }
            }

            if (this.Failed == this.IsNegated)
            {
                return;
            }

            if (!this.isRoot && this.LastError==null)
            {
                return ;
            }

            var fluentMessage = FluentMessage.BuildMessage(this.LastError);
            if (!string.IsNullOrEmpty(this.fluentSut.CustomMessage))
            {
                fluentMessage.AddCustomMessage(this.fluentSut.CustomMessage);
            }
            if (!this.Option.HasFlag(MessageOption.NoCheckedBlock))
            {
                var block = fluentMessage.On(this.fluentSut.Value, this.index);

                if (this.Option.HasFlag(MessageOption.WithType))
                {
                    block.WithType();
                }

                if (this.fluentSut.Value == null || typeof(T).IsNullable())
                {
                    block.OfType(typeof(T));
                    fluentMessage.For(typeof(T));
                }

                block.WithHashCode(this.Option.HasFlag(MessageOption.WithHash));

                if (this.fluentSut.Value is IEnumerable list && !(this.fluentSut.Value is string))
                {
                    block.WithEnumerableCount(list.Count());
                }
            }
            else
            {
                fluentMessage.For(typeof(T));
            }

            if (this.withExpected && !this.Option.HasFlag(MessageOption.NoExpectedBlock))
            {
                MessageBlock block;
                if (this.expectedKind == ValueKind.Type)
                {
                    block = fluentMessage.ExpectedType((System.Type) this.expected);
                }
                else if (this.expectedKind == ValueKind.Values)
                {
                    block = fluentMessage.ExpectedValues(this.expected, this.index)
                        .WithEnumerableCount(this.expectedCount);
                }
                else
                {
                    block = this.IsNegated ? fluentMessage.WithGivenValue(this.expected) : fluentMessage.Expected(this.expected);
                    block.WithType(this.Option.HasFlag(MessageOption.WithType));
                    block.WithHashCode(this.Option.HasFlag(MessageOption.WithHash));
                }

                if (this.expected == null)
                {
                    block.OfType(this.expectedType);
                    fluentMessage.For(this.expectedType);
                }
                else
                {
                    if (this.enforceExpectedType)
                    {
                        fluentMessage.For(this.expectedType);
                    }
                }

                if (!string.IsNullOrEmpty(this.Label))
                {
                    block.Label(this.Label);
                }
                else if (!string.IsNullOrEmpty(this.Comparison))
                {
                    block.Comparison(this.Comparison);
                }
            }
            else if (this.withGiven)
            {
                fluentMessage.WithGivenValue(this.expected).Comparison(this.Comparison);
            }

            if (!PolyFill.IsNullOrWhiteSpace(this.SutName))
            {
                fluentMessage.For(this.SutName);
            }

            this.ReportError(fluentMessage);
        }

        private void ReportError(FluentMessage fluentMessage)
        {
            this.fluentSut.Reporter.ReportError(fluentMessage.ToString());
        }

        public ICheckLogic<T> ComparingTo<TU>(TU givenValue, string comparisonInfo, string negatedComparisonInfo)
        {
            this.DefineExpectations(givenValue, true, comparisonInfo, negatedComparisonInfo);
            return this;
        }

        public ICheckLogic<T> DefineExpectedResult<TU>(TU resultValue, string labelForExpected, string negationForExpected)
        {
            this.DefineExpectations(resultValue, false, labelForExpected, negationForExpected);
            this.label = labelForExpected;
            this.negatedLabel = negationForExpected;
            return this;
        }

        public ICheckLogic<T> DefineExpectedValue<TU>(TU newExpectedValue, string comparisonMessage,
            string negatedComparison1)
        {
            this.DefineExpectations(newExpectedValue, false, comparisonMessage, negatedComparison1);
            return this;
        }

        private void DefineExpectations<TU>(TU newExpectedValue, bool isCompare, string comparisonMessage, string negatedComparison1, System.Type forceType = null)
        {
            if (this.cannotBetNegated && !string.IsNullOrEmpty(negatedComparison1))
            {
                throw new System.InvalidOperationException(this.negatedError);
            }
            this.expectedType = forceType ?? (newExpectedValue == null ? typeof(TU) : newExpectedValue.GetType());
            this.expected = newExpectedValue;
            if (isCompare)
            {
                this.withGiven = true;
            }
            else
            {
                this.withExpected = true;
            }
            this.comparison = comparisonMessage;
            this.negatedComparison = negatedComparison1;
        }

        public ICheckLogic<T> DefineExpectedType(System.Type expectedInstanceType)
        {
            this.expectedKind = ValueKind.Type;
            this.options |= MessageOption.WithType;
            return this.DefineExpectedValue(expectedInstanceType, string.Empty, this.doNotNeedNegatedMessage ? "":"different from");
        }

        public ICheckLogic<T> DefineExpectedValues(IEnumerable values, long count, string comparisonMessage = null,
            string newNegatedComparison = null)
        {
            this.DefineExpectations(values, false, comparisonMessage, newNegatedComparison, typeof(T));
            this.expectedKind = ValueKind.Values;
            this.expectedCount = count;
            return this;
        }

        public ICheckLogic<T> DefinePossibleValues<TU>(IEnumerable<TU> values, string comparisonMessage = "one of", string negatedComparison1 = "none of")
        {
            this.DefineExpectations(values, false, comparisonMessage, negatedComparison1, typeof(TU));
            this.expectedType = typeof(TU);
            this.enforceExpectedType = true;
            return this;
        }


        public ICheckLogic<T> SetValuesIndex(long indexInEnum)
        {
            this.index = indexInEnum;
            return this;
        }

        public ICheckLogic<T> OnNegateWhen(Func<T, bool> predicate, string error, MessageOption options)
        {
            if (this.negatedFailed)
            {
                return this;
            }

            if (predicate(this.fluentSut.Value))
            {
                this.negatedFailed = true;
                this.negatedError = error;
                this.negatedOption = options;
            }

            return this;
        }

        private enum ValueKind
        {
            Value,
            Type,
            Values
        }
    }
}