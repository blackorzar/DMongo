using System;
using System.Dynamic;
using System.Collections.Generic;
using System.Reflection;

using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace DMongo.Driver
{
	sealed class DBExpando : DynamicObject
	{
		private Dictionary<string, object> collection = new Dictionary<string, object>();
		private MongoDatabase _database;

		public DBExpando(MongoDatabase database)
		{
			this._database = database;
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{		   
		    string name = binder.Name.ToLower();
			if(!collection.ContainsKey(name))
				collection.Add(name,new CollectionExpando(this._database.GetCollection(name)));

		    return collection.TryGetValue(name, out result);
		}

	}

	sealed class CollectionExpando : DynamicObject
	{
		private MongoCollection _collection;
		private Dictionary<string, Type> action = new Dictionary<string, Type>();
		private DBAction dbaction = new DBAction();
		public CollectionExpando(MongoCollection collection)
		{
			this._collection = collection;

		}

		public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
	    {
			if(!action.ContainsKey(binder.Name.ToLower()))
				action.Add(binder.Name,typeof(DBAction));
			var newargs = new object[] { _collection, args};
			result = action[binder.Name].InvokeMember(binder.Name,System.Reflection.BindingFlags.InvokeMethod,null,dbaction,newargs);
			return true;
	    }
	}

	sealed class DBAction
	{
		public dynamic find(MongoCollection collection,object[] args)
		{
			Type type = args[0].GetType();

			if(type.Name.Contains("<>__AnonType")){

				PropertyInfo[] properties = type.GetProperties();
				Dictionary<string,object> dictionary = new Dictionary<string, object>();
				foreach(var property in properties)
					dictionary[property.Name] = property.GetValue(args[0],null);

				var query = new QueryDocument(dictionary);
				var result = collection.FindAs<BsonDocument>(query); 
				List<Entity> documents = new List<Entity>();

				if(result.Size() > 0)
				{
					foreach(var entity in result)
						documents.Add(new Entity(entity));
				}

				return documents;
			}
			throw new NotImplementedException("Only annonymous types are accepted for Queries.");
		}

		public void insert(MongoCollection collection, object[] args)
		{
			collection.Insert(args[0]);				
		}
	}

	sealed class Entity : DynamicObject
	{
		private Dictionary<string, object> properties = new Dictionary<string, object>();
		public Entity(BsonDocument document) 
		{
			dynamic value;
			foreach(var property in document.Names)
			{
				if(document[property].IsString)
					value = document[property].AsString;
				else if(document[property].IsBoolean)
					value = document.IsBoolean;
				else if (document[property].IsBsonNull)
					value = null;
				else
					value = null;

				properties.Add(property,value);
			}
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
	    {	
	        return properties.TryGetValue(binder.Name, out result);
	    }
			    
	    public override bool TrySetMember(SetMemberBinder binder, object value)
	    {	        
	        properties[binder.Name] = value;
	        return true;
	    }
	}
}

