﻿using System;
using System.Linq.Expressions;
using System.Reflection;
using Remotion.Linq.Parsing.ExpressionTreeVisitors;
using Restful.Data.Attributes;
using Restful.Data.Common;
using Restful.Data.Entity;
using Restful.Data.Linq;
using Restful.Data.MySql.Common;
using Restful.Data.MySql.SqlParts;
using Restful.Data.MySql.Visitors;
using Restful.Extensions;
using System.Collections.Generic;

namespace Restful.Data.MySql.Linq
{
    public class MySqlUpdateable<T> : IUpdateable<T>
    {
        #region Member
        /// <summary>
        /// Session 提供程序
        /// </summary>
        private MySqlSessionProvider provider;

        /// <summary>
        /// 参数聚合器
        /// </summary>
        private IList<object> parameters;

        /// <summary>
        /// DELETE 语句
        /// </summary>
        private MySqlUpdatePartsAggregator updatePartsAggregator;
        #endregion

        #region MySqlUpdateable
        /// <summary>
        /// 构造方法
        /// </summary>
        /// <param name="provider"></param>
        public MySqlUpdateable( MySqlSessionProvider provider )
        {
            this.provider = provider;
            this.parameters = new List<object>();
            this.updatePartsAggregator = new MySqlUpdatePartsAggregator();
            this.updatePartsAggregator.TableName = typeof( T ).Name;
        }
        #endregion

        #region Set
        /// <summary>
        /// 设置更新字段
        /// </summary>
        /// <param name="object"></param>
        /// <returns></returns>
        public IUpdateable<T> Set( object @object )
        {
            IEntityObject entity = (IEntityObject)@object;

            this.updatePartsAggregator.Set.Clear();

            PropertyInfo[] properties = @object.GetType().GetProperties();

            foreach( var property in properties )
            {
                if( Attribute.GetCustomAttributes( property, typeof( PrimaryKeyAttribute ), true ).Length > 0 ) continue;

                if( entity.ChangedProperties.Contains( property.Name ) == false ) continue;

                object value = property.EmitGetValue( @object );

                value = value == null ? DBNull.Value : value;

                this.parameters.Add( value );

                if( this.updatePartsAggregator.Set.Length == 0 )
                {
                    this.updatePartsAggregator.Set.AppendFormat("{0}{1}{2} = ?", Constants.LeftQuote, property.Name, Constants.RightQuote);
                }
                else
                {
                    this.updatePartsAggregator.Set.AppendFormat(", {0}{1}{2} = ?", Constants.LeftQuote, property.Name, Constants.RightQuote);
                }
            }

            return this;
        }
        #endregion

        #region Where
        /// <summary>
        /// 设置过滤条件
        /// </summary>
        /// <param name="func"></param>
        /// <returns></returns>
        public IUpdateable<T> Where( Expression<Func<T, bool>> func )
        {
            Expression expression = PartialEvaluatingExpressionTreeVisitor.EvaluateIndependentSubtrees( func );

            MySqlWhereClauseVisitor visitor = new MySqlWhereClauseVisitor( this.parameters );

            string whereSqlParts = visitor.Translate( expression );

            if( this.updatePartsAggregator.Where.Length == 0 )
            {
                this.updatePartsAggregator.Where.AppendFormat( " ( {0} )", whereSqlParts );
            }
            else
            {
                this.updatePartsAggregator.Where.AppendFormat( " AND ( {0} )", whereSqlParts );
            }

            return this;
        }
        #endregion

        #region Provider
        /// <summary>
        /// 
        /// </summary>
        public ISessionProvider Provider
        {
            get { return this.provider; }
        }
        #endregion

        #region Execute
        public int Execute()
        {
            SqlCmd command = new SqlCmd( this.updatePartsAggregator.ToString(), this.parameters );

            SqlCmd.Current = command;

            return this.provider.ExecuteNonQuery( command.Sql, command.Parameters );
        }
        #endregion
    }
}
