// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Dolittle.SDK.Concepts;
using Dolittle.Vanir.Backend;
using Dolittle.Vanir.Backend.Collections;
using Dolittle.Vanir.Backend.Dolittle;
using Dolittle.Vanir.Backend.Features;
using Dolittle.Vanir.Backend.GraphQL;
using Dolittle.Vanir.Backend.GraphQL.Concepts;
using Dolittle.Vanir.Backend.GraphQL.Validation;
using Dolittle.Vanir.Backend.Reflection;
using FluentValidation;
using HotChocolate;
using HotChocolate.Configuration;
using HotChocolate.Subscriptions;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;
using Microsoft.AspNetCore.Builder;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> for adding GraphQL services.
    /// </summary>
    public static class GraphQLServiceCollectionExtensions
    {
        public const char UuidFormat = 'D';

        /// <summary>
        /// Add GraphQL services.
        /// </summary>
        /// <param name="services"><see cref="IServiceColletion"/> to add to.</param>
        public static void AddGraphQL(this IServiceCollection services, IContainer container, BackendArguments arguments = null, ITypes types = null)
        {
            if (types == null)
            {
                types = new Types();
                services.Add(new ServiceDescriptor(typeof(ITypes), types));
            }

            services.AddFluentValidation(container, types);

            var graphControllers = new GraphControllers(types);
            services.Add(new ServiceDescriptor(typeof(IGraphControllers), graphControllers));

            services.AddSingleton<ITypeInspector, TypeInspector>();

            foreach (var graphControllerType in graphControllers.All)
            {
                services.Add(new ServiceDescriptor(graphControllerType, graphControllerType, ServiceLifetime.Transient));
            }

            var graphQLBuilder = services
                                    .AddGraphQLServer()
                                    .AddInMemorySubscriptions()
                                    .AddDirectiveType<FeatureDirectiveType>()
                                    .ModifyRequestOptions(opt => opt.IncludeExceptionDetails = RuntimeEnvironment.isDevelopment)
                                    .TryAddTypeInterceptor<ReadOnlyPropertyInterceptor>()
                                    .AddAuthorization()
                                    .UseFluentValidation()
                                    .AddType(new UuidType(UuidFormat));
            types.FindMultiple<ScalarType>().Where(_ => !_.IsGenericType).ForEach(_ => graphQLBuilder.AddType(_));

            var namingConventions = new NamingConventions();
            services.AddSingleton<INamingConventions>(namingConventions);

            graphQLBuilder
                .AddQueries(graphControllers, namingConventions, out SchemaRoute query)
                .AddMutations(graphControllers, namingConventions, out SchemaRoute mutations)
                .AddSubscriptions(graphControllers, namingConventions, out SchemaRoute subscriptions);

            var systemQueries = new SchemaRoute("system", "system", "_system");
            query.AddChild(systemQueries);
            Expression<Func<FeaturesSubscriptionsResolver, FeatureNotification>> featuresMethod = (FeaturesSubscriptionsResolver resolver) => resolver.Features();
            systemQueries.AddItem(new SchemaRouteItem(featuresMethod.GetMethodInfo(), "features"));

            Expression<Func<FeaturesSubscriptionsResolver, Task<FeatureNotification>>> newFeaturesMethod = (FeaturesSubscriptionsResolver resolver) => resolver.system_newFeatures(null);
            subscriptions.AddItem(new SchemaRouteItem(newFeaturesMethod.GetMethodInfo(), "system_newFeatures"));

            types.FindMultiple(typeof(ConceptAs<>)).ForEach(_ => graphQLBuilder.AddConceptTypeConverter(_));
            types.All.Where(_ => _.IsEnum).ForEach(type =>
            {
                graphQLBuilder.BindRuntimeType(type, typeof(IntType));
                graphQLBuilder.AddTypeConverter(_ => new EnumToIntConverter(type));
            });

            arguments?.GraphQLExecutorBuilder(graphQLBuilder);

            if (RuntimeEnvironment.isDevelopment)
            {
                if (arguments.ExposeEventsInGraphQLSchema) mutations.AddEventsAsMutations(types);
                graphQLBuilder.AddApolloTracing();
            }

            var scannedTypes = new Dictionary<Type, Type>();
            graphQLBuilder.RegisterAllEnumsFor(queries, scannedTypes);
            graphQLBuilder.RegisterAllEnumsFor(mutations, scannedTypes);
        }
    }
}
