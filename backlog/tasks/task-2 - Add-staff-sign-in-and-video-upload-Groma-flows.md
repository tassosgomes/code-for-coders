---
id: TASK-2
title: Add staff sign-in and video upload Groma flows
status: Done
assignee:
  - '@codex'
created_date: '2026-09-29 16:55'
updated_date: '2026-09-29 17:01'
labels: []
dependencies: []
references:
  - staff-member
  - staff-session
  - videos-area-screen
  - codeforcoders-bffadmin-api-program
  - staffsessionidentityclient
  - codeforcoders-media-api-program
  - s3-compatible-media-storage
  - codeforcoders-identity-api-program
modified_files:
  - .groma/relationships.md
  - .groma/flows/staff-member-sign-in.md
  - .groma/flows/staff-member-video-upload.md
type: docs
ordinal: 2000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The architecture map already documents student registration as a Groma flow, but staff sign-in and video upload are represented only by component relationships. Readers need separate actor-led scenarios to understand how staff authenticate and how the Admin portal sends multipart video uploads through the BFF and Media API.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 A separate Staff member sign-in flow is indexed and starts with the Staff member actor.
- [x] #2 A separate Staff member video upload flow is indexed and starts with the Staff member actor.
- [x] #3 The upload flow shows multipart URL creation, direct part upload to S3-compatible storage, and upload completion through the BFF and Media API.
- [x] #4 Every flow step is backed by a current relationship, and the Groma CLI can render both flow records.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
1. Add missing authored relationships for the staff actor, part URL and completion requests, and browser-to-storage uploads. 2. Add separate sign-in and video-upload flows that order existing relations. 3. Inspect the flow index and both rendered records; update Backlog modified-file paths and architecture references as Groma writes them.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Added Staff member → videos-area-screen relation for starting a video upload.

Added the Admin SPA part-URL request relation to the BFF video upload endpoint.

Added the direct browser part-upload relation to S3-compatible storage using presigned HTTP PUT URLs.

Added the Admin SPA upload-completion request relation to the BFF video upload endpoint.

Added the Staff member sign-in flow as a separate actor-led scenario.

Added a separate Staff member video upload flow covering multipart setup, direct storage upload, and completion.

Verification: groma view --plain lists both Staff member flows; groma view staff-member-sign-in and groma view staff-member-video-upload render their steps; focused Admin SPA, Admin BFF, Media API and S3 views confirm the modeled relationships. This is a Groma documentation change, so no application build or tests were applicable.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Added separate Staff member sign-in and video-upload flows to Groma, plus the missing actor, part-URL, direct S3 part-upload, and completion relationships. Verified the map index and both flow records with groma view; all four acceptance criteria are checked.
<!-- SECTION:FINAL_SUMMARY:END -->
