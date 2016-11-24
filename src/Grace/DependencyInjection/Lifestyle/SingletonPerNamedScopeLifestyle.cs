﻿using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Grace.DependencyInjection.Exceptions;

namespace Grace.DependencyInjection.Lifestyle
{
    /// <summary>
    /// Singleton per a named scope
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplayValue,nq}")]
    public class SingletonPerNamedScopeLifestyle : ICompiledLifestyle
    {
        private readonly string _scopeName;
 
        /// <summary>
        /// Unique id
        /// </summary>
        protected readonly string UniqueId = Guid.NewGuid().ToString();

        /// <summary>
        /// Compiled delegate
        /// </summary>
        protected ActivationStrategyDelegate CompiledDelegate;

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="scopeName"></param>
        public SingletonPerNamedScopeLifestyle(string scopeName)
        {
            _scopeName = scopeName;
        }

        /// <summary>
        /// Root the request context when creating expression
        /// </summary>
        public bool RootRequest { get; } = true;

        /// <summary>
        /// Clone the lifestyle
        /// </summary>
        /// <returns></returns>
        public ICompiledLifestyle Clone()
        {
            return new SingletonPerNamedScopeLifestyle(_scopeName);
        }

        /// <summary>
        /// Provide an expression that uses the lifestyle
        /// </summary>
        /// <param name="scope">scope for the strategy</param>
        /// <param name="request">activation request</param>
        /// <param name="activationExpression">expression to create strategy type</param>
        /// <returns></returns>
        public IActivationExpressionResult ProvideLifestlyExpression(IInjectionScope scope, IActivationExpressionRequest request,
            IActivationExpressionResult activationExpression)
        {
            if (CompiledDelegate == null)
            {
                var localDelegate = request.Services.Compiler.CompileDelegate(scope, activationExpression);

                Interlocked.CompareExchange(ref CompiledDelegate, localDelegate, null);
            }

            var getValueFromScopeMethod =
                typeof(SingletonPerNamedScopeLifestyle).GetRuntimeMethod("GetValueFromScope",
                    new[]
                    {
                        typeof(IExportLocatorScope),
                        typeof(ActivationStrategyDelegate),
                        typeof(string),
                        typeof(string),
                        typeof(StaticInjectionContext)
                    });

            var closedMethod = getValueFromScopeMethod.MakeGenericMethod(request.ActivationType);

            var expression = Expression.Call(closedMethod,
                                             request.Constants.ScopeParameter,
                                             Expression.Constant(CompiledDelegate),
                                             Expression.Constant(UniqueId),
                                             Expression.Constant(_scopeName),
                                             Expression.Constant(request.GetStaticInjectionContext()));

            return request.Services.Compiler.CreateNewResult(request, expression);
        }

        /// <summary>
        /// Get value from scope
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="scope"></param>
        /// <param name="creationDelegate"></param>
        /// <param name="uniqueId"></param>
        /// <param name="scopeName"></param>
        /// <param name="injectionContext"></param>
        /// <returns></returns>
        public static T GetValueFromScope<T>(IExportLocatorScope scope, ActivationStrategyDelegate creationDelegate,
            string uniqueId,
            string scopeName,
            StaticInjectionContext injectionContext)
        {
            while (scope != null)
            {
                if (scope.ScopeName == scopeName)
                {
                    break;
                }

                scope = scope.Parent;
            }

            if (scope == null)
            {
                throw new NamedScopeLocateException(scopeName, injectionContext);
            }

            var value = scope.GetExtraData(uniqueId);

            if (value != null)
            {
                return (T)value;
            }

            lock (scope.GetLockObject(uniqueId))
            {
                value = scope.GetExtraData(uniqueId);

                if (value == null)
                {
                    value = creationDelegate(scope, scope, null);

                    scope.SetExtraData(uniqueId, value);
                }
            }

            return (T)value;
        }

        private string DebuggerDisplayValue => $"Singleton Per Named Scope ({_scopeName})";
    }
}
