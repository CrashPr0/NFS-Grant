// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

#if VERAFile_StudyEvents
using UnityEngine;
using System;

namespace VERA
{
	
	/// <summary>
	/// Static class for recording new entries to the StudyEvents CSV file.
	/// <br/><br/>This class has been generated based on the CSV file you have defined on the VERA portal.
	/// This class should be the only way you record new CSV entries to the StudyEvents file.
	/// <br/><br/>Notably, use the CreateCsvEntry() method to create new entries in the StudyEvents CSV log file.
	/// </summary>
	public static class VERAFile_StudyEvents
	{
		
		private const string fileName = "StudyEvents";
		
		/// <summary>
		/// Creates a new row entry in the StudyEvents CSV log file.
		/// This CSV entry will automatically have the following fields populated:
		/// <list type="bullet">
		/// <item><description>pID (Participant ID)</description></item>
		/// <item><description>TS (Timestamp in milliseconds since application start)</description></item>
		/// <item><description>Conditions (Experimental conditions the participant was under during this log, in JSON format)</description></item>
		/// </list>
		/// This function has been set up according to your configuration and preferences for this file type on the VERA portal.
		/// Included in your configuration are the following additional columns:
		/// <list type="bullet">
		/// <item>timestamp_utc_ms: Value for the 'timestamp_utc_ms' column, of type float.</item>
		/// <item>platform: Value for the 'platform' column, of type string.</item>
		/// <item>event_type: Value for the 'event_type' column, of type string.</item>
		/// <item>target_id: Value for the 'target_id' column, of type string.</item>
		/// <item>detail: Value for the 'detail' column, of type string.</item>
		/// <item>world_x: Value for the 'world_x' column, of type float.</item>
		/// <item>world_y: Value for the 'world_y' column, of type float.</item>
		/// <item>world_z: Value for the 'world_z' column, of type float.</item>
		/// <item>screen_x: Value for the 'screen_x' column, of type float.</item>
		/// <item>screen_y: Value for the 'screen_y' column, of type float.</item>
		/// </list>
		/// </summary>
		/// <param name="timestamp_utc_ms">timestamp_utc_ms: Value for the 'timestamp_utc_ms' column, of type float.</param>
		/// <param name="platform">platform: Value for the 'platform' column, of type string.</param>
		/// <param name="event_type">event_type: Value for the 'event_type' column, of type string.</param>
		/// <param name="target_id">target_id: Value for the 'target_id' column, of type string.</param>
		/// <param name="detail">detail: Value for the 'detail' column, of type string.</param>
		/// <param name="world_x">world_x: Value for the 'world_x' column, of type float.</param>
		/// <param name="world_y">world_y: Value for the 'world_y' column, of type float.</param>
		/// <param name="world_z">world_z: Value for the 'world_z' column, of type float.</param>
		/// <param name="screen_x">screen_x: Value for the 'screen_x' column, of type float.</param>
		/// <param name="screen_y">screen_y: Value for the 'screen_y' column, of type float.</param>
		public static void CreateCsvEntry(float timestamp_utc_ms, string platform, string event_type, string target_id, string detail, float world_x, float world_y, float world_z, float screen_x, float screen_y)
		{
			VERASessionManager.CreateArbitraryCsvEntry(fileName, timestamp_utc_ms, platform, event_type, target_id, detail, world_x, world_y, world_z, screen_x, screen_y);
		}
		
	}
}
#endif
