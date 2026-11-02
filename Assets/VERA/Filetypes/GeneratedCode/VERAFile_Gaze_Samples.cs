// Copyright (c) 2024-2026 University of Central Florida for VERA. All rights reserved. <https://vera-xr.io>
// SPDX-FileCopyrightText: 2024-2026 University of Central Florida for VERA <https://vera-xr.io>
// SPDX-License-Identifier: LicenseRef-VERA

#if VERAFile_Gaze_Samples
using UnityEngine;
using System;

namespace VERA
{
	
	/// <summary>
	/// Static class for recording new entries to the Gaze_Samples CSV file.
	/// <br/><br/>This class has been generated based on the CSV file you have defined on the VERA portal.
	/// This class should be the only way you record new CSV entries to the Gaze_Samples file.
	/// <br/><br/>Notably, use the CreateCsvEntry() method to create new entries in the Gaze_Samples CSV log file.
	/// </summary>
	public static class VERAFile_Gaze_Samples
	{
		
		private const string fileName = "Gaze_Samples";
		
		/// <summary>
		/// Creates a new row entry in the Gaze_Samples CSV log file.
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
		/// <item>frame: Value for the 'frame' column, of type int.</item>
		/// <item>head_pos_x: Value for the 'head_pos_x' column, of type float.</item>
		/// <item>head_pos_y: Value for the 'head_pos_y' column, of type float.</item>
		/// <item>head_pos_z: Value for the 'head_pos_z' column, of type float.</item>
		/// <item>head_rot_x: Value for the 'head_rot_x' column, of type float.</item>
		/// <item>head_rot_y: Value for the 'head_rot_y' column, of type float.</item>
		/// <item>head_rot_z: Value for the 'head_rot_z' column, of type float.</item>
		/// <item>head_rot_w: Value for the 'head_rot_w' column, of type float.</item>
		/// <item>gaze_origin_x: Value for the 'gaze_origin_x' column, of type float.</item>
		/// <item>gaze_origin_y: Value for the 'gaze_origin_y' column, of type float.</item>
		/// <item>gaze_origin_z: Value for the 'gaze_origin_z' column, of type float.</item>
		/// <item>gaze_dir_x: Value for the 'gaze_dir_x' column, of type float.</item>
		/// <item>gaze_dir_y: Value for the 'gaze_dir_y' column, of type float.</item>
		/// <item>gaze_dir_z: Value for the 'gaze_dir_z' column, of type float.</item>
		/// <item>gaze_source: Value for the 'gaze_source' column, of type string.</item>
		/// <item>gaze_confidence: Value for the 'gaze_confidence' column, of type float.</item>
		/// <item>angular_velocity_deg_s: Value for the 'angular_velocity_deg_s' column, of type float.</item>
		/// <item>is_fixating: Value for the 'is_fixating' column, of type int.</item>
		/// <item>fixation_id: Value for the 'fixation_id' column, of type int.</item>
		/// <item>hit_target: Value for the 'hit_target' column, of type string.</item>
		/// <item>hit_point_x: Value for the 'hit_point_x' column, of type float.</item>
		/// <item>hit_point_y: Value for the 'hit_point_y' column, of type float.</item>
		/// <item>hit_point_z: Value for the 'hit_point_z' column, of type float.</item>
		/// <item>hit_distance: Value for the 'hit_distance' column, of type float.</item>
		/// </list>
		/// </summary>
		/// <param name="timestamp_utc_ms">timestamp_utc_ms: Value for the 'timestamp_utc_ms' column, of type float.</param>
		/// <param name="frame">frame: Value for the 'frame' column, of type int.</param>
		/// <param name="head_pos_x">head_pos_x: Value for the 'head_pos_x' column, of type float.</param>
		/// <param name="head_pos_y">head_pos_y: Value for the 'head_pos_y' column, of type float.</param>
		/// <param name="head_pos_z">head_pos_z: Value for the 'head_pos_z' column, of type float.</param>
		/// <param name="head_rot_x">head_rot_x: Value for the 'head_rot_x' column, of type float.</param>
		/// <param name="head_rot_y">head_rot_y: Value for the 'head_rot_y' column, of type float.</param>
		/// <param name="head_rot_z">head_rot_z: Value for the 'head_rot_z' column, of type float.</param>
		/// <param name="head_rot_w">head_rot_w: Value for the 'head_rot_w' column, of type float.</param>
		/// <param name="gaze_origin_x">gaze_origin_x: Value for the 'gaze_origin_x' column, of type float.</param>
		/// <param name="gaze_origin_y">gaze_origin_y: Value for the 'gaze_origin_y' column, of type float.</param>
		/// <param name="gaze_origin_z">gaze_origin_z: Value for the 'gaze_origin_z' column, of type float.</param>
		/// <param name="gaze_dir_x">gaze_dir_x: Value for the 'gaze_dir_x' column, of type float.</param>
		/// <param name="gaze_dir_y">gaze_dir_y: Value for the 'gaze_dir_y' column, of type float.</param>
		/// <param name="gaze_dir_z">gaze_dir_z: Value for the 'gaze_dir_z' column, of type float.</param>
		/// <param name="gaze_source">gaze_source: Value for the 'gaze_source' column, of type string.</param>
		/// <param name="gaze_confidence">gaze_confidence: Value for the 'gaze_confidence' column, of type float.</param>
		/// <param name="angular_velocity_deg_s">angular_velocity_deg_s: Value for the 'angular_velocity_deg_s' column, of type float.</param>
		/// <param name="is_fixating">is_fixating: Value for the 'is_fixating' column, of type int.</param>
		/// <param name="fixation_id">fixation_id: Value for the 'fixation_id' column, of type int.</param>
		/// <param name="hit_target">hit_target: Value for the 'hit_target' column, of type string.</param>
		/// <param name="hit_point_x">hit_point_x: Value for the 'hit_point_x' column, of type float.</param>
		/// <param name="hit_point_y">hit_point_y: Value for the 'hit_point_y' column, of type float.</param>
		/// <param name="hit_point_z">hit_point_z: Value for the 'hit_point_z' column, of type float.</param>
		/// <param name="hit_distance">hit_distance: Value for the 'hit_distance' column, of type float.</param>
		public static void CreateCsvEntry(float timestamp_utc_ms, int frame, float head_pos_x, float head_pos_y, float head_pos_z, float head_rot_x, float head_rot_y, float head_rot_z, float head_rot_w, float gaze_origin_x, float gaze_origin_y, float gaze_origin_z, float gaze_dir_x, float gaze_dir_y, float gaze_dir_z, string gaze_source, float gaze_confidence, float angular_velocity_deg_s, int is_fixating, int fixation_id, string hit_target, float hit_point_x, float hit_point_y, float hit_point_z, float hit_distance)
		{
			VERASessionManager.CreateArbitraryCsvEntry(fileName, timestamp_utc_ms, frame, head_pos_x, head_pos_y, head_pos_z, head_rot_x, head_rot_y, head_rot_z, head_rot_w, gaze_origin_x, gaze_origin_y, gaze_origin_z, gaze_dir_x, gaze_dir_y, gaze_dir_z, gaze_source, gaze_confidence, angular_velocity_deg_s, is_fixating, fixation_id, hit_target, hit_point_x, hit_point_y, hit_point_z, hit_distance);
		}
		
	}
}
#endif
