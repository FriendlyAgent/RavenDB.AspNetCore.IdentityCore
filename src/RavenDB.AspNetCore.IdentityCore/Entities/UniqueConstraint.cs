using System;
using System.Linq.Expressions;
using System.Reflection;

namespace RavenDB.AspNetCore.IdentityCore.Entities
{
    /// <summary>
    /// The class responsible for unique values.
    /// </summary>
    /// <typeparam name="TInstance">the type of class.</typeparam>
    public class UniqueConstraint<TInstance>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="UniqueConstraint{TInstance}"/>.
        /// </summary>
        /// <param name="value">The value that need to be unique.</param>
        /// <param name="func">The property that contains the unique value.</param>
        public UniqueConstraint(
            string value,
            Expression<Func<TInstance, string>> func)
        {
            EntityTypeKey = typeof(TInstance).Name;
            ConstraintValue = value;
            EntityPropertyKey = GetPropertyName(func);
        }

        /// <summary>
        /// The unique identifier.
        /// </summary>
        public string Id
        {
            get
            {
                return string.Format("UniqueConstraints/{0}/{1}/{2}", EntityTypeKey, EntityPropertyKey, ConstraintValue);
            }
        }

        /// <summary>
        /// The name of the entity.
        /// </summary>
        public string EntityTypeKey { get; private set; }

        /// <summary>
        /// The name of the property.
        /// </summary>
        public string EntityPropertyKey { get; private set; }

        /// <summary>
        /// Relation with the record that wants to have a unique property.
        /// </summary>
        public string RelationId { get; set; }

        /// <summary>
        /// The unique value.
        /// </summary>
        public string ConstraintValue { get; private set; }

        private string GetPropertyName(Expression<Func<TInstance, string>> func)
        {
            if (func == null)
                throw new ArgumentNullException("func");

            var memberExpr = func.Body as MemberExpression;
            if (memberExpr == null)
            {
                var unaryExpr = func.Body as UnaryExpression;
                if (unaryExpr != null && unaryExpr.NodeType == ExpressionType.Convert)
                    memberExpr = unaryExpr.Operand as MemberExpression;
            }

            if (memberExpr != null && memberExpr.Member.MemberType == MemberTypes.Property)
                return memberExpr.Member.Name;

            throw new ArgumentNullException("PropertyExtensions", "Unable to get Property Name");
        }
    }
}