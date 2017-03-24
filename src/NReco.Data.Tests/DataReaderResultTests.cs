using System;

using Xunit;
namespace NReco.Data.Tests
{

	public class DataReaderResultTests
	{
		[Fact]
		public void ReadToDictionary() {
			var rs = RecordSetTests.generateRecordSet();

			var firstRecord = new DataReaderResult(new RecordSetReader(rs)).ToDictionary();
			Assert.NotNull(firstRecord);
			Assert.Equal(0, firstRecord["id"]);
			Assert.Equal(4, firstRecord.Count);

			var allRecords = new DataReaderResult(new RecordSetReader(rs)).ToDictionaryList();
			Assert.Equal(100, allRecords.Count);
		}

		[Fact]
		public void ReadToRecordSet() {
			var rs = RecordSetTests.generateRecordSet();

			var allRecords = new DataReaderResult(new RecordSetReader(rs)).ToRecordSet();
			Assert.Equal(4, allRecords.Columns.Count);
			Assert.Equal(0, allRecords[0]["id"]);
			Assert.Equal(100, allRecords.Count);

			var allRecordsAsyncRes = new DataReaderResult(new RecordSetReader(rs)).ToRecordSetAsync().Result;
			Assert.Equal(100, allRecordsAsyncRes.Count);
		}

		[Fact]
		public void ReadToModel() {
			var rs = RecordSetTests.generateRecordSet();

			var secondId = new DataReaderResult(new RecordSetReader(rs), 1, 1).Single<int>();
			Assert.Equal(1, secondId);

			var firstRecord = new DataReaderResult(new RecordSetReader(rs)).Single<TestModel>();
			Assert.NotNull(firstRecord);
			Assert.Equal(0, firstRecord.id);

			var allRecords = new DataReaderResult(new RecordSetReader(rs)).ToList<TestModel>();
			Assert.Equal(100, allRecords.Count);
		}

		public class TestModel {
			public int id { get; set; }
			public string name { get; set; }
			public decimal amount { get; set; }
			public DateTime added_date { get; set; }
		}


	}
}
