﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RawBencher.Benchers
{
	/// <summary>
	/// Abstract base class for the benchers. Each bencher inherits from this class to be used in the bench run.
	/// </summary>
	/// <typeparam name="T">The type of the element fetched by the framework this bencher is for, e.g. an entity class type</typeparam>
	public abstract class BencherBase<T> : IBencher
	{
		#region Member declarations
		private Func<T, int> _salesOrderIdRetriever;
		#endregion


		/// <summary>
		/// Initializes a new instance of the <see cref="BencherBase{T}" /> class.
		/// </summary>
		/// <param name="salesOrderIdRetriever">The salesOrderId retriever func, to be used in the data verification step.</param>
		/// <param name="performsChangeTracking">if set to <c>true</c> the fetches will be resulting in change tracked elements</param>
		/// <param name="performsCaching">if set to <c>true</c> the fetches will be using some form of caching (resultset caching, element caching)</param>
		/// <exception cref="System.ArgumentNullException">salesOrderIdRetriever</exception>
		protected BencherBase(Func<T, int> salesOrderIdRetriever, bool usesChangeTracking, bool usesCaching)
		{
			if(salesOrderIdRetriever == null)
			{
				throw new ArgumentNullException("salesOrderIdRetriever");
			}
			_salesOrderIdRetriever = salesOrderIdRetriever;
			this.UsesCaching = usesCaching;
			this.UsesChangeTracking = usesChangeTracking;
		}


		/// <summary>
		/// Fetches the individual element
		/// </summary>
		/// <typeparam name="T">Type of the element to fetch</typeparam>
		/// <param name="key">The key of the element to fetch.</param>
		/// <returns>The fetched element, or null if not found</returns>
		public abstract T FetchIndividual(int key);
		/// <summary>
		/// Fetches the complete set of elements and returns this set as an IEnumerable.
		/// </summary>
		/// <returns>
		/// the set fetched
		/// </returns>
		public abstract IEnumerable<T> FetchSet();
		/// <summary>
		/// Creates the name of the framework this bencher is for. Use the overload which accepts a format string and a type to create a name based on a
		/// specific version
		/// </summary>
		/// <returns>the framework name.</returns>
		public abstract string CreateFrameworkName();


		/// <summary>
		/// Performs the individual bench mark. This is a benchmark which fetches all SalesOrderHeader elements with the keys specified, individually.
		/// </summary>
		/// <param name="keys">The keys for all elements to fetch.</param>
		/// <returns>
		/// A filled in benchmark result object, with the total time taken to fetch all elements of the keys specified. EnumerationTime is not set. Number of
		/// </returns>
		public BenchResult PerformIndividualBenchMark(List<int> keys)
		{
			var toReturn = new BenchResult();
			int numberOfElementsFetched = 0;
			var sw = new Stopwatch();
			sw.Start();
			foreach(var key in keys)
			{
				var element = FetchIndividual(key);
				var verifyResult = VerifyElement(element);
				if(verifyResult > 0)
				{
					numberOfElementsFetched++;
				}
			}
			sw.Stop();
			toReturn.FetchTimeInMilliseconds = sw.ElapsedMilliseconds;
			toReturn.NumberOfRowsFetched = numberOfElementsFetched;
			return toReturn;
		}


		/// <summary>
		/// Performs the set benchmark. This is a benchmark which fetches the full set of sales order headers, enumerates it and returns the times it took
		/// to perform these actions as well as the number of rows read.
		/// </summary>
		/// <returns>A filled in benchmark result object</returns>
		public BenchResult PerformSetBenchmark()
		{
			var toReturn = new BenchResult();
			var sw = new Stopwatch();
			sw.Start();
			var headers = FetchSet();
			sw.Stop();
			toReturn.FetchTimeInMilliseconds = sw.ElapsedMilliseconds;
			sw.Reset();
			sw.Start();
			toReturn.NumberOfRowsFetched = VerifyData(headers);
			sw.Stop();
			toReturn.EnumerationTimeInMilliseconds = sw.ElapsedMilliseconds;
			return toReturn;
		}


		/// <summary>
		/// Creates the name of the framework this bencher is for. Use it in the implementation of CreateFrameworkName
		/// </summary>
		/// <param name="formatString">The format string to use. Expects {0} to be for assembly version, {1} for file version.</param>
		/// <param name="t">The type in the assembly to use for file/assembly version.</param>
		/// <returns>ready to use framework name</returns>
		protected string CreateFrameworkName(string formatString, Type t)
		{
			string assemblyVersion;
			string fileVersion;
			GetVersionStrings(t.Assembly, out fileVersion, out assemblyVersion);
			return string.Format(formatString, assemblyVersion, fileVersion);
		}


		/// <summary>
		/// Verifies the data in toEnumerate. Uses the salesorderid retriever set in the ctor to obtain the SalesOrderId from each element in the enumerable
		/// specified
		/// </summary>
		/// <param name="toEnumerate">To enumerate.</param>
		/// <returns>the total number of elements enumerated, or -1 if the row contains no data (the func returned a value less or equal to 0)</returns>
		private int VerifyData(IEnumerable<T> toEnumerate)
		{
			int amount = 0;
			foreach(var v in toEnumerate)
			{
				if(VerifyElement(v) <= 0)
				{
					return -1;
				}
				amount++;
			}
			return amount;
		}


		/// <summary>
		/// Verifies the element specified, by calling the set SalesOrderId func and passing the specified element to it. The func should return
		/// a value higher than 0
		/// </summary>
		/// <param name="toVerify">To verify.</param>
		/// <returns>the value returned from the func, or -1 if failed</returns>
		private int VerifyElement(T toVerify)
		{
			if(toVerify == null)
			{
				return -1;
			}
			return _salesOrderIdRetriever(toVerify);
		}


		/// <summary>
		/// Gets the version strings to be used in the framework version name
		/// </summary>
		/// <param name="toProbe">assembly to use for version/ fileversion retrieval</param>
		/// <param name="fileVersion">The file version, as defined in the specified assembly.</param>
		/// <param name="assemblyVersion">The assembly version of the specified assembly, in 4 digits.</param>
		private void GetVersionStrings(Assembly toProbe, out string fileVersion, out string assemblyVersion)
		{
			fileVersion = string.Empty;
			assemblyVersion = string.Empty;
			if(toProbe != null)
			{
				assemblyVersion = toProbe.GetName().Version.ToString();
				fileVersion = FileVersionInfo.GetVersionInfo(toProbe.Location).FileVersion;
			}
		}


		#region Properties
		/// <summary>
		/// Gets a value indicating whether the fetch uses some form of caching (resultset caching, element caching)
		/// </summary>
		public bool UsesCaching { get; private set; }
		/// <summary>
		/// Gets a value indicating whether the fetch results in change tracked elements or not. 
		/// </summary>
		public bool UsesChangeTracking { get; private set; }
		#endregion
	}
}