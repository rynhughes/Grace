﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Grace.DependencyInjection;
using Grace.DependencyInjection.Impl;
using Grace.Tests.Classes.Simple;
using Xunit;

namespace Grace.Tests.DependencyInjection.AddOns
{
    public class StrategyInspectorTests
    {
        public class StrategyInspectorInjectProperty : IActivationStrategyInspector
        {
            public void Inspect<T>(T strategy) where T : class, IActivationStrategy
            {
                var exportStrategy = strategy as ICompiledExportStrategy;

                if (exportStrategy != null)
                {
                    foreach (var propertyInfo in strategy.ActivationType.GetProperties())
                    {
                        if (propertyInfo.CanWrite &&
                            propertyInfo.SetMethod.IsPublic &&
                           !propertyInfo.SetMethod.IsStatic)
                        {
                            var injectionInfo = new MemberInjectionInfo {MemberInfo = propertyInfo};

                            if (strategy.InjectionScope.ScopeConfiguration.Behaviors.KeyedTypeSelector(
                                    propertyInfo.PropertyType))
                            {
                                injectionInfo.LocateKey = propertyInfo.Name;
                            }
                            
                            exportStrategy.MemberInjectionSelector(new PropertyMemberInjectionSelector(injectionInfo));
                        }
                    }
                }
            }
        }

        [Fact]
        public void StrategyInspectorInjection_Tests()
        {
            var container = new DependencyInjectionContainer();

            container.Configure(c =>
            {
                c.AddInspector(new StrategyInspectorInjectProperty());
                c.Export<BasicService>().As<IBasicService>();
                c.Export<PropertyInjectionService>().As<IPropertyInjectionService>();
            });

            var instance = container.Locate<IPropertyInjectionService>(new { count = 5 });

            Assert.NotNull(instance);
            Assert.NotNull(instance.BasicService);
        }
    }
}

