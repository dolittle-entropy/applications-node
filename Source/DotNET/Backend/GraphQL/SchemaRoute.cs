// Copyright (c) Dolittle. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Dolittle.Vanir.Backend.Features;
using HotChocolate.Types;
using Microsoft.AspNetCore.Authorization;

namespace Dolittle.Vanir.Backend.GraphQL
{
    public class SchemaRoute : ObjectType
    {
        readonly List<SchemaRoute> _children = new();
        readonly List<SchemaRouteItem> _items = new();

        public SchemaRoute(string path, string localName, string typeName)
        {
            Path = path;
            LocalName = localName;
            TypeName = typeName;
        }

        public string Path { get; }
        public string LocalName { get; }
        public string TypeName { get; }
        public bool HasItems => _items.Count > 0;

        public IEnumerable<SchemaRoute> Children => _children;
        public IEnumerable<SchemaRouteItem> Items => _items;

        public void AddChild(SchemaRoute child)
        {
            _children.Add(child);
        }

        public void AddItem(SchemaRouteItem item)
        {
            _items.Add(item);
        }

        protected override void Configure(IObjectTypeDescriptor descriptor)
        {
            descriptor.Name(TypeName);

            foreach (var item in _items)
            {
                var fieldDescriptor = descriptor.Field(item.Method).Name(item.Name);

                AddAdornedFeatures(item, fieldDescriptor);

                AddAdornedAuthorization(item, fieldDescriptor);
            }

            foreach (var child in _children)
            {
                descriptor.Field(child.LocalName).Type(child).Resolver(_ => new object());
            }

            if (_items.Count == 0)
            {
                descriptor.Field("Default").Resolve(() => "Configure your first item");
            }
        }

        void AddAdornedFeatures(SchemaRouteItem item, IObjectFieldDescriptor fieldDescriptor)
        {
            var featureAttributes = new List<FeatureAttribute>();
            featureAttributes.AddRange(item.Method.GetCustomAttributes(typeof(FeatureAttribute), true) as FeatureAttribute[]);
            featureAttributes.AddRange(item.Method.DeclaringType.GetCustomAttributes(typeof(FeatureAttribute), true) as FeatureAttribute[]);

            if (featureAttributes.Count > 0)
            {
                foreach (var featureAttribute in featureAttributes)
                {
                    fieldDescriptor.Directive(new FeatureDirective { Name = featureAttribute.Name });
                }
            }
        }

        void AddAdornedAuthorization(SchemaRouteItem item, IObjectFieldDescriptor fieldDescriptor)
        {
            var authorizeAttributes = new List<AuthorizeAttribute>();
            authorizeAttributes.AddRange(item.Method.GetCustomAttributes(typeof(AuthorizeAttribute), true) as AuthorizeAttribute[]);
            authorizeAttributes.AddRange(item.Method.DeclaringType.GetCustomAttributes(typeof(AuthorizeAttribute), true) as AuthorizeAttribute[]);

            foreach (var authorizeAttribute in authorizeAttributes)
            {
                if (string.IsNullOrEmpty(authorizeAttribute.Policy))
                {
                    fieldDescriptor.Authorize();
                }
                else
                {
                    fieldDescriptor.Authorize(authorizeAttribute.Policy);
                }
            }
        }
    }
}
