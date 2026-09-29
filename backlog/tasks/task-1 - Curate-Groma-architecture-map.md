---
id: TASK-1
title: Curate Groma architecture map
status: Done
assignee:
  - '@codex'
created_date: '2026-09-29 13:56'
updated_date: '2026-09-29 15:28'
labels: []
dependencies: []
references:
  - README.md
  - vision.md
  - context/architecture-baseline.md
  - docker-compose.yml
  - identity
  - codeforcoders-identity-api-program
  - serviceassertionextensions
  - staffsessiontokenissuer
  - codeforcoders-identity-application-dependencyinjection
  - resolveauditidentityreferences
  - identitydbcontext
  - codeforcoders-identity-infra-messaging-dependencyinjection
  - codeforcoders-audit-application-dependencyinjection
  - auditdbcontext
  - codeforcoders-audit-infra-messaging-dependencyinjection
  - codeforcoders-audit-api
  - codeforcoders-commerce-application-dependencyinjection
  - codeforcoders-commerce-infra-data-dependencyinjection
  - codeforcoders-commerce-infra-messaging-dependencyinjection
  - codeforcoders-commerce-api
  - codeforcoders-learning-application-dependencyinjection
  - codeforcoders-learning-infra-data-dependencyinjection
  - codeforcoders-learning-infra-messaging-dependencyinjection
  - codeforcoders-learning-api
  - codeforcoders-media-application-dependencyinjection
  - codeforcoders-media-infra-data-dependencyinjection
  - codeforcoders-media-infra-messaging-dependencyinjection
  - codeforcoders-media-api
  - codeforcoders-notification-infra-messaging-dependencyinjection
  - codeforcoders-notification-application-dependencyinjection
  - codeforcoders-notification-api
  - codeforcoders-notification-infra-data-dependencyinjection
  - codeforcoders-bffadmin-application-dependencyinjection
  - codeforcoders-bffadmin-api
  - codeforcoders-bffadmin-infra-data-dependencyinjection
  - codeforcoders-bffadmin-infra-messaging-dependencyinjection
  - codeforcoders-bffstudent-infra-data-dependencyinjection
  - codeforcoders-bffstudent-api
  - codeforcoders-bffstudent-application-dependencyinjection
  - codeforcoders-bffstudent-infra-messaging-dependencyinjection
  - admin-spa
  - student-spa
  - admin-dashboard-components-dashboard-screen
  - audit-trail-screen
  - finance-area-screen
  - staff-access-screen
  - staff-password-recovery-screen
  - staff-password-reset-screen
  - staff-session
  - videos-area-screen
  - admin-spa-src-components-app-shell
  - button
  - password-policy-schema
  - student-dashboard-components-dashboard-screen
  - student-confirmation-screen
  - student-password-change-screen
  - student-password-recovery-screen
  - student-registration-screen
  - student-session
  - student-spa-src-components-app-shell
  - codeforcoders-audit-api-program
  - codeforcoders-commerce-api-program
  - financeareaauthorization
  - codeforcoders-learning-api-program
  - codeforcoders-media-api-program
  - mediajwksconfigurationmanager
  - codeforcoders-notification-api-program
  - notificationsendrequestedmessagehandler
  - staffsessionidentityclient
  - codeforcoders-bffadmin-api-program
  - codeforcoders-bffadmin-api-security-bffsecurityextensions
  - studentsessionidentityclient
  - codeforcoders-bffstudent-api-program
  - codeforcoders-bffstudent-api-security-bffsecurityextensions
  - project
  - code-for-coders
  - codeforcoders-bffadmin-api-security-serviceassertiontokenfactory
  - codeforcoders-bffstudent-api-security-serviceassertiontokenfactory
  - password-requirements
  - student
  - staff-member
  - postgresql
  - rabbitmq
  - valkey
  - s3-compatible-media-storage
  - email-delivery-provider
  - opentelemetry-collector
modified_files:
  - .groma/actors/staff-member.md
  - .groma/actors/student.md
  - .groma/externals/email-delivery-provider.md
  - .groma/externals/opentelemetry-collector.md
  - .groma/externals/postgresql.md
  - .groma/externals/rabbitmq.md
  - .groma/externals/s3-compatible-media-storage.md
  - .groma/externals/valkey.md
  - .groma/flows/deliver-an-accepted-notification.md
  - .groma/flows/student-account-registration.md
  - .groma/project.md
  - .groma/relationships.md
  - .groma/scanners.json
  - .groma/systems/code-for-coders/components/20260920232840-initialaudit.md
  - >-
    .groma/systems/code-for-coders/components/20260921184003-adddeliveryrecords.md
  - >-
    .groma/systems/code-for-coders/components/20260921194427-adddeliveryrecordrefusal.md
  - >-
    .groma/systems/code-for-coders/components/20260921213348-addtransactionalemailretrystate.md
  - >-
    .groma/systems/code-for-coders/components/20260921225912-adddeliveryoutcomecounters.md
  - >-
    .groma/systems/code-for-coders/components/20260922141129-addprocessingnamespace.md
  - >-
    .groma/systems/code-for-coders/components/20260923142831-addstudentregistration.md
  - >-
    .groma/systems/code-for-coders/components/20260923162915-addstudentsessions.md
  - >-
    .groma/systems/code-for-coders/components/20260924165555-rebuildadministrativeactrecords.md
  - >-
    .groma/systems/code-for-coders/components/20260924174803-recordnonconformingadministrativeacts.md
  - >-
    .groma/systems/code-for-coders/components/20260924184735-secureauditruntimepermissions.md
  - .groma/systems/code-for-coders/components/20260925160336-addstaffaccounts.md
  - >-
    .groma/systems/code-for-coders/components/20260925172750-addstaffsessionidempotency.md
  - >-
    .groma/systems/code-for-coders/components/20260925183745-addstaffinvitations.md
  - >-
    .groma/systems/code-for-coders/components/20260925183757-addstaffinvitationrole.md
  - >-
    .groma/systems/code-for-coders/components/20260925204842-addstaffroleactionidempotencyresult.md
  - .groma/systems/code-for-coders/components/20260926161851-addvideos.md
  - .groma/systems/code-for-coders/components/20260926183642-addvideouploads.md
  - >-
    .groma/systems/code-for-coders/components/20260926203121-expirependingvideouploads.md
  - >-
    .groma/systems/code-for-coders/components/20260926220704-preparevideoplaybackassets.md
  - >-
    .groma/systems/code-for-coders/components/20260926231129-guardvideopreparationlease.md
  - >-
    .groma/systems/code-for-coders/components/20260927003719-backfillcompletedvideometadata.md
  - >-
    .groma/systems/code-for-coders/components/20260927150805-auditrecordsearchindexes.md
  - >-
    .groma/systems/code-for-coders/components/20260927173038-addauditrecordcomplements.md
  - >-
    .groma/systems/code-for-coders/components/20260927183405-addauditcomplementconfirmation.md
  - >-
    .groma/systems/code-for-coders/components/20260927195237-addoutboxpublisherleases.md
  - >-
    .groma/systems/code-for-coders/components/20260927195254-scopeauditcomplementconfirmationuniqueness.md
  - .groma/systems/code-for-coders/components/acceptnotificationsendrequest.md
  - >-
    .groma/systems/code-for-coders/components/acceptnotificationsendrequestinput.md
  - >-
    .groma/systems/code-for-coders/components/acceptnotificationsendrequestinputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/acceptnotificationsendrequestoutput.md
  - .groma/systems/code-for-coders/components/acceptstaffinvitation.md
  - .groma/systems/code-for-coders/components/acceptstaffinvitationinput.md
  - >-
    .groma/systems/code-for-coders/components/acceptstaffinvitationinputvalidator.md
  - .groma/systems/code-for-coders/components/acceptstaffinvitationoutput.md
  - .groma/systems/code-for-coders/components/account.md
  - .groma/systems/code-for-coders/components/accountconfiguration.md
  - .groma/systems/code-for-coders/components/accounttype.md
  - .groma/systems/code-for-coders/components/admin-spa-eslint-config.md
  - >-
    .groma/systems/code-for-coders/components/admin-spa-public-runtime-env-template.md
  - .groma/systems/code-for-coders/components/admin-spa-src-testing-handlers.md
  - .groma/systems/code-for-coders/components/admin-spa-src-testing-server.md
  - .groma/systems/code-for-coders/components/admin-spa-src-testing-setup.md
  - >-
    .groma/systems/code-for-coders/components/admin-spa-src-testing-test-utils.md
  - >-
    .groma/systems/code-for-coders/components/admin-spa-src-utils-get-error-message.md
  - .groma/systems/code-for-coders/components/admin-spa-vite-config.md
  - .groma/systems/code-for-coders/components/admin-spa-vitest-config.md
  - .groma/systems/code-for-coders/components/administrativeact.md
  - .groma/systems/code-for-coders/components/administrativeactpolicy.md
  - .groma/systems/code-for-coders/components/administrativeactreference.md
  - .groma/systems/code-for-coders/components/aesvideokeyprotector.md
  - .groma/systems/code-for-coders/components/assets-eslint-config.md
  - .groma/systems/code-for-coders/components/ativoprontov1.md
  - .groma/systems/code-for-coders/components/atopraticado.md
  - .groma/systems/code-for-coders/components/atopraticadojsonconverter.md
  - .groma/systems/code-for-coders/components/auditappendonlypolicy.md
  - >-
    .groma/systems/code-for-coders/components/auditcomplementconfirmationstore.md
  - .groma/systems/code-for-coders/components/auditcomplementconfirmationv1.md
  - .groma/systems/code-for-coders/components/auditcomplementconsumerworker.md
  - >-
    .groma/systems/code-for-coders/components/auditcomplementidempotencyrecord.md
  - >-
    .groma/systems/code-for-coders/components/auditcomplementidempotencyrecordconfiguration.md
  - .groma/systems/code-for-coders/components/auditcomplementrecordstatus.md
  - .groma/systems/code-for-coders/components/auditdatabaseoptions.md
  - .groma/systems/code-for-coders/components/auditdbcontext.md
  - .groma/systems/code-for-coders/components/auditdbcontextfactory.md
  - .groma/systems/code-for-coders/components/auditdbcontextmodelsnapshot.md
  - .groma/systems/code-for-coders/components/auditeventconsumerworker.md
  - .groma/systems/code-for-coders/components/auditfilterinvalidexception.md
  - .groma/systems/code-for-coders/components/auditidentityreferencequeries.md
  - .groma/systems/code-for-coders/components/auditrecord.md
  - >-
    .groma/systems/code-for-coders/components/auditrecordalreadyexistsexception.md
  - .groma/systems/code-for-coders/components/auditrecordconfiguration.md
  - .groma/systems/code-for-coders/components/auditrecorddetailqueries.md
  - .groma/systems/code-for-coders/components/auditrecordmessagesv1.md
  - .groma/systems/code-for-coders/components/auditrecordsearchqueries.md
  - .groma/systems/code-for-coders/components/auditrecordsnapshotstore.md
  - .groma/systems/code-for-coders/components/auditrecordwriter.md
  - .groma/systems/code-for-coders/components/auditschema.md
  - .groma/systems/code-for-coders/components/auditsnapshotconnectionprovider.md
  - .groma/systems/code-for-coders/components/auditsnapshothealthcheck.md
  - .groma/systems/code-for-coders/components/auditsnapshotoptions.md
  - >-
    .groma/systems/code-for-coders/components/auditsnapshotunavailableexception.md
  - .groma/systems/code-for-coders/components/audittelemetry.md
  - .groma/systems/code-for-coders/components/audittopologyinitializer.md
  - .groma/systems/code-for-coders/components/auditunitofwork.md
  - .groma/systems/code-for-coders/components/authenticatestaffsession.md
  - .groma/systems/code-for-coders/components/authenticatestaffsessioninput.md
  - >-
    .groma/systems/code-for-coders/components/authenticatestaffsessioninputvalidator.md
  - .groma/systems/code-for-coders/components/authenticatestaffsessionoutput.md
  - .groma/systems/code-for-coders/components/authenticatestudentsession.md
  - .groma/systems/code-for-coders/components/authenticatestudentsessioninput.md
  - >-
    .groma/systems/code-for-coders/components/authenticatestudentsessioninputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/authenticatestudentsessionoutput.md
  - .groma/systems/code-for-coders/components/awsmediaoptions.md
  - .groma/systems/code-for-coders/components/bffadmindbcontext.md
  - .groma/systems/code-for-coders/components/bffadmindbcontextfactory.md
  - .groma/systems/code-for-coders/components/bffadmindbcontextmodelsnapshot.md
  - .groma/systems/code-for-coders/components/bffadminschema.md
  - .groma/systems/code-for-coders/components/bffadmintelemetry.md
  - .groma/systems/code-for-coders/components/bffadminunitofwork.md
  - .groma/systems/code-for-coders/components/bffstudentdbcontext.md
  - .groma/systems/code-for-coders/components/bffstudentdbcontextfactory.md
  - >-
    .groma/systems/code-for-coders/components/bffstudentdbcontextmodelsnapshot.md
  - .groma/systems/code-for-coders/components/bffstudentschema.md
  - .groma/systems/code-for-coders/components/bffstudenttelemetry.md
  - .groma/systems/code-for-coders/components/bffstudentunitofwork.md
  - .groma/systems/code-for-coders/components/changestaffrole.md
  - .groma/systems/code-for-coders/components/changestaffroleinput.md
  - .groma/systems/code-for-coders/components/changestudentpassword.md
  - .groma/systems/code-for-coders/components/changestudentpasswordinput.md
  - >-
    .groma/systems/code-for-coders/components/changestudentpasswordinputvalidator.md
  - .groma/systems/code-for-coders/components/changestudentpasswordoutput.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-application-exceptions-notfoundexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-application-exceptions-relatedaggregateexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-application-exceptions-usecaseexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-application-interfaces-iunitofwork.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-application-usecases-iusecase.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-domain-seedwork-entityvalidationexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-domain-seedwork-tenantid.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-infra-data-health-postgreshealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-infra-messaging-configuration-rabbitmqoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-infra-messaging-health-rabbitmqhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-infra-messaging-rabbitmqconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-audit-infra-messaging-rabbitmqtelemetry.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-common-bffsecurityoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-common-itenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-common-opaquebffsession.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-common-tenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-exceptions-notfoundexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-exceptions-relatedaggregateexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-exceptions-usecaseexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-interfaces-ibffsessionstore.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-interfaces-ioutboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-interfaces-iunitofwork.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-usecases-iusecase.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-application-usecases…1238
    tokens truncated…dmin-application-dependencyinjection
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-contracts-platformheartbeatv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-contracts-staffinvitationmessagesv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-contracts-staffmembermessagesv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-contracts-staffpasswordrecoveryrequestv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-contracts-staffsessionmessagesv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-data-configuration-outboxprotectionoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-data-configuration-valkeyoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-data-health-outboxhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-data-health-postgreshealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-data-health-valkeyconnectionprovider.md…775
    tokens truncated…ffadmin-infra-data-dependencyinjection
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-messaging-configuration-outboxoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-messaging-configuration-rabbitmqoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-messaging-health-rabbitmqhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-messaging-heartbeatconsumerworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-messaging-heartbeatreceiptstore.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffadmin-infra-messaging-outboxpublisherworker.…705
    tokens truncated…cyinjection
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-common-bffsecurityoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-common-itenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-common-opaquebffsession.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-common-tenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-exceptions-notfoundexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-exceptions-relatedaggregateexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-exceptions-usecaseexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-interfaces-ibffsessionstore.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-interfaces-ioutboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-interfaces-iunitofwork.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-application-usecases-iusecase.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-contracts-platformheartbeatv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-contracts-studentpasswordchangev1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-contracts-studentregistrationrequestv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-contracts-studentsessionmessagesv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-data-configuration-valkeyoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-data-health-outboxhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-data-health-postgreshealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-data-health-valkeyconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-data-health-valkeyhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-data-migrations-20260920213007-initialoutbox.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-data-outbox-outboxmessage.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-data-outbox-outboxmessageconfiguration.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-data-outbox-outboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-data-valkeybffsessionstore.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-configuration-outboxoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-configuration-rabbitmqoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-health-rabbitmqhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-heartbeatconsumerworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-heartbeatreceiptstore.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-outboxpublisherworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-outboxpublishexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-rabbitmqconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-rabbitmqpublisher.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-rabbitmqtelemetry.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-bffstudent-infra-messaging-rabbitmqtopologyinitializer.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-b…926 tokens
    truncated… codeforcoders-bffstudent-application-dependencyinjection
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-common-itenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-common-tenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-exceptions-notfoundexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-exceptions-relatedaggregateexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-exceptions-usecaseexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-interfaces-ioutboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-interfaces-iunitofwork.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-usecases-iusecase.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-usecases-platform-recordplatformheartbeat-irecordplatformheartbeat.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeat.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatinput.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatinputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatoutput.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-domain-seedwork-entityvalidationexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-domain-seedwork-tenantid.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-data-configuration-valkeyoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-data-health-outboxhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-data-health-postgreshealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-data-health-valkeyconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-data-health-valkeyhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-data-migrations-20260920213007-initialoutbox.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-data-outbox-outboxmessage.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-data-outbox-outboxmessageconfiguration.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-data-outbox-outboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-configuration-outboxoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-configuration-rabbitmqoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-health-rabbitmqhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-heartbeatconsumerworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-heartbeatreceiptstore.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-outboxpublisherworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-outboxpublishexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-rabbitmqconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-rabbitmqpublisher.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-rabbitmqtelemetry.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-commerce-infra-messaging-rabbitmqtopologyinitializer.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-common-itenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-common-tenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-exceptions-notfoundexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-exceptions-relatedaggregateexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-exceptions-usecaseexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-interfaces-ioutboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-interfaces-iunitofwork.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-usecases-iusecase.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-usecases-platform-recordplatformheartbeat-irecordplatformheartbeat.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeat.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatinput.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatinputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatoutput.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-contracts-platformheartbeatv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-contracts-staffinvitationmessagesv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-contracts-staffpasswordrecoveryrequestv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-contracts-staffsessionmessagesv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-contracts-studentpasswordchangev1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-contracts-studentregistrationrequestv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-contracts-studentsessionmessagesv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-domain-seedwork-entityvalidationexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-domain-seedwork-tenantid.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-configuration-outboxprotectionoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-configuration-valkeyoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-health-outboxhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-health-postgreshealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-health-valkeyconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-health-valkeyhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-migrations-20260920213007-initialoutbox.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-outbox-outboxmessage.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-outbox-outboxmessageconfiguration.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-outbox-outboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-data-outbox-outboxpayloadprotector.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-configuration-outboxoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-configuration-rabbitmqoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-health-rabbitmqhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-heartbeatconsumerworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-heartbeatreceiptstore.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-outboxpublisherworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-outboxpublishexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-rabbitmqconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-rabbitmqpublisher.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-rabbitmqtelemetry.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-identity-infra-messaging-rabbitmqtopologyinitializer.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-common-itenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-common-tenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-exceptions-notfoundexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-exceptions-relatedaggregateexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-exceptions-usecaseexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-interfaces-ioutboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-interfaces-iunitofwork.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-usecases-iusecase.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-usecases-platform-recordplatformheartbeat-irecordplatformheartbeat.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeat.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatinput.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatinputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatoutput.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-domain-seedwork-entityvalidationexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-domain-seedwork-tenantid.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-data-configuration-valkeyoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-data-health-outboxhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-data-health-postgreshealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-data-health-valkeyconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-data-health-valkeyhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-data-migrations-20260920213007-initialoutbox.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-data-outbox-outboxmessage.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-data-outbox-outboxmessageconfiguration.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-data-outbox-outboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-configuration-outboxoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-configuration-rabbitmqoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-health-rabbitmqhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-heartbeatconsumerworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-heartbeatreceiptstore.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-outboxpublisherworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-outboxpublishexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-rabbitmqconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-rabbitmqpublisher.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-rabbitmqtelemetry.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-learning-infra-messaging-rabbitmqtopologyinitializer.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-common-itenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-common-tenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-exceptions-notfoundexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-exceptions-relatedaggregateexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-exceptions-usecaseexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-interfaces-ioutboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-interfaces-iunitofwork.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-usecases-iusecase.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-usecases-platform-recordplatformheartbeat-irecordplatformheartbeat.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeat.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatinput.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatinputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatoutput.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-contracts-platformheartbeatv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-domain-seedwork-entityvalidationexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-domain-seedwork-tenantid.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-data-configuration-valkeyoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-data-health-outboxhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-data-health-postgreshealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-data-health-valkeyconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-data-health-valkeyhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-data-migrations-20260920213007-initialoutbox.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-data-outbox-outboxmessage.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-data-outbox-outboxmessageconfiguration.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-data-outbox-outboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-configuration-outboxoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-configuration-rabbitmqoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-health-rabbitmqhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-heartbeatconsumerworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-heartbeatreceiptstore.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-outboxpublisherworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-outboxpublishexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-rabbitmqconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-rabbitmqpublisher.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-rabbitmqtelemetry.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-media-infra-messaging-rabbitmqtopologyinitializer.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-common-itenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-common-tenantcontext.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-exceptions-notfoundexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-exceptions-relatedaggregateexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-exceptions-usecaseexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-interfaces-ioutboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-interfaces-iunitofwork.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-usecases-iusecase.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-usecases-platform-recordplatformheartbeat-irecordplatformheartbeat.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeat.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatinput.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatinputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-application-usecases-platform-recordplatformheartbeat-recordplatformheartbeatoutput.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-contracts-platformheartbeatv1.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-domain-seed…1870
    tokens truncated…n-dependencyinjection
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-data-configuration-valkeyoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-data-health-outboxhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-data-health-postgreshealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-data-health-valkeyconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-data-health-valkeyhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-data-migrations-20260920213007-initialoutbox.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-data-outbox-outboxmessage.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-data-outbox-outboxmessageconfiguration.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-data-outbox-outboxmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-configuration-outboxoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-configuration-rabbitmqoptions.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-health-rabbitmqhealthcheck.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-heartbeatconsumerworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-heartbeatreceiptstore.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-outboxpublisherworker.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-outboxpublishexception.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-rabbitmqconnectionprovider.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-rabbitmqpublisher.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-rabbitmqtelemetry.md
  - >-
    .groma/systems/code-for-coders/components/codeforcoders-notification-infra-messaging-rabbitmqtopologyinitializer.md
  - .groma/systems/code-for-coders/components/commercedbcontext.md
  - .groma/systems/code-for-coders/components/commercedbcontextfactory.md
  - .groma/systems/code-for-coders/components/commercedbcontextmodelsnapshot.md
  - .groma/systems/code-for-coders/components/commercemodules.md
  - .groma/systems/code-for-coders/components/commerceplatformheartbeatv1.md
  - .groma/systems/code-for-coders/components/commerceschemas.md
  - .groma/systems/code-for-coders/components/commercetelemetry.md
  - .groma/systems/code-for-coders/components/commerceunitofwork.md
  - .groma/systems/code-for-coders/components/complementoconfirmadov1.md
  - .groma/systems/code-for-coders/components/completevideoupload.md
  - .groma/systems/code-for-coders/components/concurrencyconflictexception.md
  - .groma/systems/code-for-coders/components/concurrentwriteexception.md
  - .groma/systems/code-for-coders/components/confirmstudentaccount.md
  - .groma/systems/code-for-coders/components/confirmstudentaccountinput.md
  - >-
    .groma/systems/code-for-coders/components/confirmstudentaccountinputvalidator.md
  - .groma/systems/code-for-coders/components/confirmstudentaccountoutput.md
  - .groma/systems/code-for-coders/components/createstaffinvitation.md
  - .groma/systems/code-for-coders/components/createstaffinvitationinput.md
  - >-
    .groma/systems/code-for-coders/components/createstaffinvitationinputvalidator.md
  - .groma/systems/code-for-coders/components/createstaffinvitationoutput.md
  - .groma/systems/code-for-coders/components/createvideoupload.md
  - .groma/systems/code-for-coders/components/createvideouploadparturls.md
  - .groma/systems/code-for-coders/components/credential.md
  - .groma/systems/code-for-coders/components/credentialconfiguration.md
  - .groma/systems/code-for-coders/components/deliveryoptions.md
  - >-
    .groma/systems/code-for-coders/components/deliveryoutcomecounterconfiguration.md
  - >-
    .groma/systems/code-for-coders/components/deliveryoutcomecounterrepository.md
  - .groma/systems/code-for-coders/components/deliveryrecordconfiguration.md
  - .groma/systems/code-for-coders/components/deliveryrecordpurgeworker.md
  - .groma/systems/code-for-coders/components/deliveryrecordrepository.md
  - .groma/systems/code-for-coders/components/deliveryrecordretentionoptions.md
  - .groma/systems/code-for-coders/components/diagnose-admin-login.md
  - .groma/systems/code-for-coders/components/emailoptions.md
  - .groma/systems/code-for-coders/components/expiredvideouploadworker.md
  - .groma/systems/code-for-coders/components/expirependingvideouploads.md
  - .groma/systems/code-for-coders/components/ffmpegvideotranscoder.md
  - .groma/systems/code-for-coders/components/get-admin-workspace-status.md
  - .groma/systems/code-for-coders/components/get-student-workspace-status.md
  - .groma/systems/code-for-coders/components/getauditrecord.md
  - .groma/systems/code-for-coders/components/getauditrecordinput.md
  - .groma/systems/code-for-coders/components/getauditrecordoutput.md
  - .groma/systems/code-for-coders/components/getvideo.md
  - .groma/systems/code-for-coders/components/getvideoinput.md
  - .groma/systems/code-for-coders/components/getvideoupload.md
  - .groma/systems/code-for-coders/components/grantstaffrole.md
  - .groma/systems/code-for-coders/components/grantstaffroleinput.md
  - .groma/systems/code-for-coders/components/iacceptstaffinvitation.md
  - .groma/systems/code-for-coders/components/iauditactrecorder.md
  - .groma/systems/code-for-coders/components/iauditcomplementrecorder.md
  - .groma/systems/code-for-coders/components/iauditidentityreferencequeries.md
  - .groma/systems/code-for-coders/components/iauditrecorddetailqueries.md
  - .groma/systems/code-for-coders/components/iauditrecordsearchqueries.md
  - .groma/systems/code-for-coders/components/iauditrecordwriter.md
  - .groma/systems/code-for-coders/components/iauthenticatestaffsession.md
  - .groma/systems/code-for-coders/components/iauthenticatestudentsession.md
  - .groma/systems/code-for-coders/components/ichangestaffrole.md
  - .groma/systems/code-for-coders/components/ichangestudentpassword.md
  - .groma/systems/code-for-coders/components/icompletevideoupload.md
  - .groma/systems/code-for-coders/components/iconfirmstudentaccount.md
  - .groma/systems/code-for-coders/components/icreatestaffinvitation.md
  - .groma/systems/code-for-coders/components/icreatevideoupload.md
  - .groma/systems/code-for-coders/components/icreatevideouploadparturls.md
  - .groma/systems/code-for-coders/components/idempotencyfingerprinter.md
  - .groma/systems/code-for-coders/components/idempotencyoptions.md
  - .groma/systems/code-for-coders/components/idempotencyrecord.md
  - .groma/systems/code-for-coders/components/idempotencyrecordconfiguration.md
  - .groma/systems/code-for-coders/components/identityconfirmationstore.md
  - .groma/systems/code-for-coders/components/identitydbcontext.md
  - .groma/systems/code-for-coders/components/identitydbcontextfactory.md
  - .groma/systems/code-for-coders/components/identitydbcontextmodelsnapshot.md
  - .groma/systems/code-for-coders/components/identitypasswordrecoverystore.md
  - .groma/systems/code-for-coders/components/identityregistrationstore.md
  - .groma/systems/code-for-coders/components/identityschema.md
  - .groma/systems/code-for-coders/components/identitysessionstore.md
  - .groma/systems/code-for-coders/components/identitystaffaccountstore.md
  - .groma/systems/code-for-coders/components/identitystaffinvitationstore.md
  - .groma/systems/code-for-coders/components/identitytelemetry.md
  - .groma/systems/code-for-coders/components/identityunitofwork.md
  - .groma/systems/code-for-coders/components/iexpirependingvideouploads.md
  - .groma/systems/code-for-coders/components/igetauditrecord.md
  - .groma/systems/code-for-coders/components/igetvideo.md
  - .groma/systems/code-for-coders/components/igetvideoupload.md
  - .groma/systems/code-for-coders/components/igrantstaffrole.md
  - .groma/systems/code-for-coders/components/iidempotencyfingerprinter.md
  - .groma/systems/code-for-coders/components/iidentityconfirmationstore.md
  - .groma/systems/code-for-coders/components/iidentitypasswordrecoverystore.md
  - .groma/systems/code-for-coders/components/iidentityregistrationstore.md
  - .groma/systems/code-for-coders/components/iidentitysessionstore.md
  - .groma/systems/code-for-coders/components/iidentitystaffaccountstore.md
  - .groma/systems/code-for-coders/components/iidentitystaffinvitationstore.md
  - .groma/systems/code-for-coders/components/ilistpendingstaffinvitations.md
  - .groma/systems/code-for-coders/components/ilistpendingvideouploads.md
  - .groma/systems/code-for-coders/components/iliststaffmembers.md
  - .groma/systems/code-for-coders/components/ilistvideos.md
  - .groma/systems/code-for-coders/components/ilookupstaffinvitation.md
  - .groma/systems/code-for-coders/components/imediastorageport.md
  - .groma/systems/code-for-coders/components/inspect-admin-session.md
  - .groma/systems/code-for-coders/components/ioperationidempotencyrepository.md
  - .groma/systems/code-for-coders/components/ipasswordhasher.md
  - .groma/systems/code-for-coders/components/iprovisionfirstadministrator.md
  - .groma/systems/code-for-coders/components/irecordadministrativeact.md
  - .groma/systems/code-for-coders/components/iregisterstudentaccount.md
  - .groma/systems/code-for-coders/components/irequeststaffpasswordreset.md
  - >-
    .groma/systems/code-for-coders/components/irequeststudentaccountconfirmation.md
  - .groma/systems/code-for-coders/components/irequeststudentpasswordreset.md
  - .groma/systems/code-for-coders/components/iresetstaffpassword.md
  - .groma/systems/code-for-coders/components/iresetstudentpassword.md
  - .groma/systems/code-for-coders/components/iresolveauditidentityreferences.md
  - .groma/systems/code-for-coders/components/irevokestaffrole.md
  - .groma/systems/code-for-coders/components/irevokestaffsession.md
  - .groma/systems/code-for-coders/components/irevokestudentsession.md
  - .groma/systems/code-for-coders/components/isearchauditrecords.md
  - .groma/systems/code-for-coders/components/iserviceassertionreplaystore.md
  - .groma/systems/code-for-coders/components/istaffinvitationmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/istaffpasswordrecoverymessagewriter.md
  - .groma/systems/code-for-coders/components/istaffrolemessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/istudentconfirmationmessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/istudentpasswordrecoverymessagewriter.md
  - >-
    .groma/systems/code-for-coders/components/istudentregistrationmessagewriter.md
  - .groma/systems/code-for-coders/components/iupdatevideotitle.md
  - .groma/systems/code-for-coders/components/ivalidatestaffsession.md
  - .groma/systems/code-for-coders/components/ivalidatestudentsession.md
  - .groma/systems/code-for-coders/components/ivideokeyprotector.md
  - .groma/systems/code-for-coders/components/ivideopreparationrepository.md
  - .groma/systems/code-for-coders/components/ivideopreparationworkflow.md
  - .groma/systems/code-for-coders/components/ivideoqueries.md
  - .groma/systems/code-for-coders/components/ivideotranscoder.md
  - .groma/systems/code-for-coders/components/ivideouploadrepository.md
  - .groma/systems/code-for-coders/components/learningdbcontext.md
  - .groma/systems/code-for-coders/components/learningdbcontextfactory.md
  - .groma/systems/code-for-coders/components/learningdbcontextmodelsnapshot.md
  - .groma/systems/code-for-coders/components/learningmodules.md
  - .groma/systems/code-for-coders/components/learningplatformheartbeatv1.md
  - .groma/systems/code-for-coders/components/learningschemas.md
  - .groma/systems/code-for-coders/components/learningtelemetry.md
  - .groma/systems/code-for-coders/components/learningunitofwork.md
  - .groma/systems/code-for-coders/components/listpendingstaffinvitations.md
  - >-
    .groma/systems/code-for-coders/components/listpendingstaffinvitationsinput.md
  - >-
    .groma/systems/code-for-coders/components/listpendingstaffinvitationsinputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/listpendingstaffinvitationsoutput.md
  - .groma/systems/code-for-coders/components/listpendingvideouploads.md
  - .groma/systems/code-for-coders/components/liststaffmembers.md
  - .groma/systems/code-for-coders/components/liststaffmembersinput.md
  - .groma/systems/code-for-coders/components/liststaffmembersinputvalidator.md
  - .groma/systems/code-for-coders/components/listvideos.md
  - .groma/systems/code-for-coders/components/listvideosinput.md
  - .groma/systems/code-for-coders/components/lookupstaffinvitation.md
  - .groma/systems/code-for-coders/components/lookupstaffinvitationinput.md
  - >-
    .groma/systems/code-for-coders/components/lookupstaffinvitationinputvalidator.md
  - .groma/systems/code-for-coders/components/lookupstaffinvitationoutput.md
  - .groma/systems/code-for-coders/components/mediaapiexception.md
  - .groma/systems/code-for-coders/components/mediadbcontext.md
  - .groma/systems/code-for-coders/components/mediadbcontextfactory.md
  - .groma/systems/code-for-coders/components/mediadbcontextmodelsnapshot.md
  - .groma/systems/code-for-coders/components/mediaroleoptions.md
  - .groma/systems/code-for-coders/components/mediaschema.md
  - .groma/systems/code-for-coders/components/mediatelemetry.md
  - .groma/systems/code-for-coders/components/mediaunitofwork.md
  - .groma/systems/code-for-coders/components/mediavolumemetricsworker.md
  - >-
    .groma/systems/code-for-coders/components/multipartuploadnotfoundexception.md
  - >-
    .groma/systems/code-for-coders/components/notificationsendrequestconsumerworker.md
  - >-
    .groma/systems/code-for-coders/components/operationidempotencyconfiguration.md
  - .groma/systems/code-for-coders/components/operationidempotencyrecord.md
  - .groma/systems/code-for-coders/components/operationidempotencyrepository.md
  - .groma/systems/code-for-coders/components/outboxdestinationoptions.md
  - .groma/systems/code-for-coders/components/pbkdf2passwordhasher.md
  - .groma/systems/code-for-coders/components/preparacaofalhouv1.md
  - .groma/systems/code-for-coders/components/prepare-and-smoke-nonadmin.md
  - .groma/systems/code-for-coders/components/preparevideo.md
  - .groma/systems/code-for-coders/components/provisionfirstadministrator.md
  - >-
    .groma/systems/code-for-coders/components/provisionfirstadministratorinput.md
  - >-
    .groma/systems/code-for-coders/components/provisionfirstadministratorinputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/provisionfirstadministratoroutput.md
  - >-
    .groma/systems/code-for-coders/components/provisionfirstadministratorstatus.md
  - .groma/systems/code-for-coders/components/rabbitmqresourcenames.md
  - .groma/systems/code-for-coders/components/recordadministrativeact.md
  - .groma/systems/code-for-coders/components/recordadministrativeactinput.md
  - .groma/systems/code-for-coders/components/recordadministrativeactoutput.md
  - .groma/systems/code-for-coders/components/recordauditcomplement.md
  - .groma/systems/code-for-coders/components/referenciaato.md
  - .groma/systems/code-for-coders/components/registerstudentaccount.md
  - .groma/systems/code-for-coders/components/registerstudentaccountinput.md
  - >-
    .groma/systems/code-for-coders/components/registerstudentaccountinputvalidator.md
  - .groma/systems/code-for-coders/components/registerstudentaccountoutput.md
  - .groma/systems/code-for-coders/components/registrationoptions.md
  - >-
    .groma/systems/code-for-coders/components/registrationwriteconflictexception.md
  - .groma/systems/code-for-coders/components/requeststaffpasswordreset.md
  - .groma/systems/code-for-coders/components/requeststaffpasswordresetinput.md
  - >-
    .groma/systems/code-for-coders/components/requeststaffpasswordresetinputvalidator.md
  - .groma/systems/code-for-coders/components/requeststaffpasswordresetoutput.md
  - >-
    .groma/systems/code-for-coders/components/requeststudentaccountconfirmation.md
  - >-
    .groma/systems/code-for-coders/components/requeststudentaccountconfirmationinput.md
  - >-
    .groma/systems/code-for-coders/components/requeststudentaccountconfirmationinputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/requeststudentaccountconfirmationoutput.md
  - .groma/systems/code-for-coders/components/requeststudentpasswordreset.md
  - >-
    .groma/systems/code-for-coders/components/requeststudentpasswordresetinput.md
  - >-
    .groma/systems/code-for-coders/components/requeststudentpasswordresetinputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/requeststudentpasswordresetoutput.md
  - .groma/systems/code-for-coders/components/resetstaffpassword.md
  - .groma/systems/code-for-coders/components/resetstaffpasswordinput.md
  - >-
    .groma/systems/code-for-coders/components/resetstaffpasswordinputvalidator.md
  - .groma/systems/code-for-coders/components/resetstaffpasswordoutput.md
  - .groma/systems/code-for-coders/components/resetstudentpassword.md
  - .groma/systems/code-for-coders/components/resetstudentpasswordinput.md
  - >-
    .groma/systems/code-for-coders/components/resetstudentpasswordinputvalidator.md
  - .groma/systems/code-for-coders/components/resetstudentpasswordoutput.md
  - .groma/systems/code-for-coders/components/resolveauditidentityreferences.md
  - >-
    .groma/systems/code-for-coders/components/resolveauditidentityreferencesinput.md
  - >-
    .groma/systems/code-for-coders/components/resolveauditidentityreferencesinputvalidator.md
  - >-
    .groma/systems/code-for-coders/components/resolveauditidentityreferencesoutput.md
  - .groma/systems/code-for-coders/components/revokestaffrole.md
  - .groma/systems/code-for-coders/components/revokestaffroleinput.md
  - .groma/systems/code-for-coders/components/revokestaffsession.md
  - .groma/systems/code-for-coders/components/revokestaffsessioninput.md
  - >-
    .groma/systems/code-for-coders/components/revokestaffsessioninputvalidator.md
  - .groma/systems/code-for-coders/components/revokestudentsession.md
  - .groma/systems/code-for-coders/components/revokestudentsessioninput.md
  - >-
    .groma/systems/code-for-coders/components/revokestudentsessioninputvalidator.md
  - .groma/systems/code-for-coders/components/s3mediaclientpair.md
  - .groma/systems/code-for-coders/components/s3mediastorageadapter.md
  - .groma/systems/code-for-coders/components/searchauditrecords.md
  - .groma/systems/code-for-coders/components/searchauditrecordsinput.md
  - >-
    .groma/systems/code-for-coders/components/searchauditrecordsinputvalidator.md
  - .groma/systems/code-for-coders/components/searchauditrecordsoutput.md
  - .groma/systems/code-for-coders/components/serviceassertionreplaystore.md
  - .groma/systems/code-for-coders/components/smoke-admin-complement.md
  - .groma/systems/code-for-coders/components/smoke-admin-confirmation.md
  - .groma/systems/code-for-coders/components/staffaccountoptions.md
  - .groma/systems/code-for-coders/components/staffinvitation.md
  - .groma/systems/code-for-coders/components/staffinvitationconfiguration.md
  - .groma/systems/code-for-coders/components/staffinvitationexception.md
  - .groma/systems/code-for-coders/components/staffinvitationmessagewriter.md
  - .groma/systems/code-for-coders/components/staffinvitationoptions.md
  - .groma/systems/code-for-coders/components/staffmemberoutput.md
  - .groma/systems/code-for-coders/components/staffmemberpageoutput.md
  - .groma/systems/code-for-coders/components/staffmemberpaginationoutput.md
  - .groma/systems/code-for-coders/components/staffpasswordrecoveryexception.md
  - >-
    .groma/systems/code-for-coders/components/staffpasswordrecoverymessagewriter.md
  - .groma/systems/code-for-coders/components/staffpasswordresetinputv1.md
  - .groma/systems/code-for-coders/components/staffroleactioncommand.md
  - .groma/systems/code-for-coders/components/staffroleactioncommandvalidator.md
  - .groma/systems/code-for-coders/components/staffroleactionexception.md
  - .groma/systems/code-for-coders/components/staffroleactionexecutor.md
  - .groma/systems/code-for-coders/components/staffroleactionoutput.md
  - .groma/systems/code-for-coders/components/staffroleassignment.md
  - >-
    .groma/systems/code-for-coders/components/staffroleassignmentconfiguration.md
  - .groma/systems/code-for-coders/components/staffrolecatalog.md
  - .groma/systems/code-for-coders/components/staffrolechangecommand.md
  - .groma/systems/code-for-coders/components/staffrolechangecommandvalidator.md
  - .groma/systems/code-for-coders/components/staffrolemessagewriter.md
  - .groma/systems/code-for-coders/components/staffsession.md
  - .groma/systems/code-for-coders/components/staffsessionconfiguration.md
  - .groma/systems/code-for-coders/components/staffsessiondetails.md
  - .groma/systems/code-for-coders/components/staffsessionexception.md
  - .groma/systems/code-for-coders/components/staffsessionoptions.md
  - .groma/systems/code-for-coders/components/storageunavailableexception.md
  - .groma/systems/code-for-coders/components/student-spa-eslint-config.md
  - >-
    .groma/systems/code-for-coders/components/student-spa-public-runtime-env-template.md
  - >-
    .groma/systems/code-for-coders/components/student-spa-src-stores-use-shell-store.md
  - >-
    .groma/systems/code-for-coders/components/student-spa-src-testing-handlers.md
  - .groma/systems/code-for-coders/components/student-spa-src-testing-server.md
  - .groma/systems/code-for-coders/components/student-spa-src-testing-setup.md
  - >-
    .groma/systems/code-for-coders/components/student-spa-src-testing-test-utils.md
  - >-
    .groma/systems/code-for-coders/components/student-spa-src-utils-get-error-message.md
  - .groma/systems/code-for-coders/components/student-spa-vite-config.md
  - .groma/systems/code-for-coders/components/student-spa-vitest-config.md
  - .groma/systems/code-for-coders/components/studentaccountmessagesv1.md
  - .groma/systems/code-for-coders/components/studentconfirmationexception.md
  - >-
    .groma/systems/code-for-coders/components/studentconfirmationmessagewriter.md
  - .groma/systems/code-for-coders/components/studentpasswordchangeexception.md
  - .groma/systems/code-for-coders/components/studentpasswordpolicy.md
  - >-
    .groma/systems/code-for-coders/components/studentpasswordrecoveryexception.md
  - >-
    .groma/systems/code-for-coders/components/studentpasswordrecoverymessagewriter.md
  - .groma/systems/code-for-coders/components/studentregistrationexception.md
  - >-
    .groma/systems/code-for-coders/components/studentregistrationmessagewriter.md
  - .groma/systems/code-for-coders/components/studentsession.md
  - .groma/systems/code-for-coders/components/studentsessionconfiguration.md
  - .groma/systems/code-for-coders/components/studentsessiondetails.md
  - .groma/systems/code-for-coders/components/studentsessionexception.md
  - .groma/systems/code-for-coders/components/studentsessionoptions.md
  - >-
    .groma/systems/code-for-coders/components/stud…471 tokens
    truncated…pendencyinjection
  - >-
    .groma/systems/code-for-coders/components/transactionalemaildeliveryworker.md
  - .groma/systems/code-for-coders/components/transactionalemailretrypolicy.md
  - .groma/systems/code-for-coders/components/transactionalnotificationv1.md
  - .groma/systems/code-for-coders/components/updatevideotitle.md
  - .groma/systems/code-for-coders/components/updatevideotitleinput.md
  - .groma/systems/code-for-coders/components/validatestaffsession.md
  - .groma/systems/code-for-coders/components/validatestaffsessioninput.md
  - >-
    .groma/systems/code-for-coders/components/validatestaffsessioninputvalidator.md
  - .groma/systems/code-for-coders/components/validatestaffsessionoutput.md
  - .groma/systems/code-for-coders/components/validatestudentsession.md
  - .groma/systems/code-for-coders/components/validatestudentsessioninput.md
  - >-
    .groma/systems/code-for-coders/components/validatestudentsessioninputvalidator.md
  - .groma/systems/code-for-coders/components/validatestudentsessionoutput.md
  - .groma/systems/code-for-coders/components/verificationtoken.md
  - .groma/systems/code-for-coders/components/verificationtokenconfiguration.md
  - .groma/systems/code-for-coders/components/video.md
  - .groma/systems/code-for-coders/components/videoconfiguration.md
  - .groma/systems/code-for-coders/components/videocreateinput.md
  - .groma/systems/code-for-coders/components/videofailurereasons.md
  - .groma/systems/code-for-coders/components/videooutput.md
  - .groma/systems/code-for-coders/components/videopreparationoptions.md
  - .groma/systems/code-for-coders/components/videopreparationrepository.md
  - .groma/systems/code-for-coders/components/videopreparationworker.md
  - .groma/systems/code-for-coders/components/videoqualityladder.md
  - .groma/systems/code-for-coders/components/videoqueries.md
  - .groma/systems/code-for-coders/components/videoupload.md
  - .groma/systems/code-for-coders/components/videouploadconfiguration.md
  - .groma/systems/code-for-coders/components/videouploadoutputs.md
  - .groma/systems/code-for-coders/components/videouploadrepository.md
  - >-
    .groma/systems/code-for-coders/components/videouploadruleviolationexception.md
  - .groma/systems/code-for-coders/components/videouploadusecasehelpers.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-dashboard-components-dashboard-screen.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-layout-route.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-public-runtime-env-template.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-app-app.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-app-providers.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-app-router.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-app-routes-dashboard-route.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-app-routes-route-error.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-components-app-shell.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-components-auth-layout.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-config-env.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-config-paths.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-hooks-use-document-title.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-lib…304
    tokens truncated…use-shell-store
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/admin-spa-src-utils-get-error-message.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/audit-record-detail-route.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/audit-record-detail-screen.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/audit-trail-forbidden.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/audit-trail-navigation.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/audit-trail-route.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/audit-trail-screen.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/complete-video-upload.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/confirm-audit-record-complement.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/create-video-upload-part-urls.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/create-video-upload.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/edit-video-title-dialog.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/finance-area-route.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/finance-area-screen.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/get-admin-workspace-status.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/get-audit-record.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/get-finance-area.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/get-pending-video-uploads.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/get-staff-areas.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/get-video-upload.md
  - .groma/systems/code-for-coders/containers/admin-spa/components/get-videos.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/password-requirements.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/pending-video-uploads-alert.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/request-staff-password-reset.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/reset-staff-password.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/search-audit-records.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-access-route.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-access-screen.md
  - .groma/systems/code-for-coders/containers/admin-spa/components/staff-area.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-invitation-acceptance-route.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-invitation-acceptance-screen.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-invitation-acceptance.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-invitations.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-login-route.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-login-screen.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-members.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-password-recovery-route.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-password-recovery-screen.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-password-reset-route.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-password-reset-screen.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-session-loader.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/staff-session.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/update-video-title.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/video-status-badge.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/video-transfer-panel.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/video-upload-dialog.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/video-upload.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/videos-area-route.md
  - >-
    .groma/systems/code-for-coders/containers/admin-spa/components/videos-area-screen.md
  - .groma/systems/code-for-coders/containers/admin-spa/container.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/auditdbcontext.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/auditrecordsearchv1.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/auditsmokeendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/auditsmokeresponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/audittokensoptions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/codeforcoders-audit-api-endpoints-auditrecordendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/codeforcoders-audit-api-exceptionhandlers-globalexceptionhandler.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/codeforcoders-audit-api-extensions-endpointextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/codeforcoders-audit-api-extensions-errorhandlingextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/codeforcoders-audit-api-program.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/codeforcoders-audit-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/components/codeforcoders-audit-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-audit-api/container.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/auditapioptions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/auditidentityreferenceclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/auditrecordclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/bffsessionstatusresponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-apimodels-financearearesponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-apimodels-platformheartbeatresponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-endpoints-auditrecordendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-endpoints-financeareaendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-endpoints-platformendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-endpoints-proxyendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-endpoints-sessionendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-program.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-security-bffsecurityextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-security-bffsecuritymiddleware.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-security-bffsessioncontext.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-security-bffsessiontransformprovider.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-security-csrfprotection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-api-security-serviceassertiontokenfactory.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/codeforcoders-bffadmin-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/commerceapioptions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/commercefinanceareaclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/iauditidentityreferenceclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/iauditrecordclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/icommercefinanceareaclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/istaffinvitationidentityclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/istaffmemberidentityclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/staffidentityextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/staffidentityoptions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/staffsessioncookie.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/staffsessionidentityclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/staffsessionresponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/components/…712
    tokens truncated…ient
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api/container.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffadmin-api…1214
    tokens
    truncated…deforcoders-bffadmin-api-endpoints-staffpasswordrecoveryendpoints
    -> codeforcoders-bffadmin-api-program
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-apimodels-platformheartbeatresponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-endpoints-platformendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-endpoints-proxyendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-endpoints-sessionendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-endpoints-studentconfirmationendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-endpoints-studentpasswordchangeendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-endpoints-studentpasswordrecoveryendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-program.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-security-bffsecurityextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-security-bffsecuritymiddleware.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-security-bffsessioncontext.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-security-bffsessiontransformprovider.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-security-csrfprotection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-api-security-serviceassertiontokenfactory.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/codeforcoders-bffstudent-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/istudentpasswordchangeidentityclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/istudentpasswordrecoveryidentityclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/istudentregistrationidentityclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/istudentsessionidentityclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/studentconfirmationresult.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/studentidentityoptions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/studentpasswordchangeidentityclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/studentpasswordchangeresult.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/studentpasswordrecoveryidentityclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/studentpasswordrecoveryresult.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/studentregistrationendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/studentre…194
    tokens truncated…ffected: studentpasswordchangeidentityclient
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/studentsessioncookie.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/studentsessionidentityclient.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/components/studentspacorsoptions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-bffstudent-api/container.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/codeforcoders-commerce-api-apimodels-financearearesponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/codeforcoders-commerce-api-apimodels-platformheartbeatresponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/codeforcoders-commerce-api-endpoints-financeareaendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/codeforcoders-commerce-api-endpoints-platformendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/codeforcoders-commerce-api-exceptionhandlers-globalexceptionhandler.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/codeforcoders-commerce-api-extensions-endpointextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/codeforcoders-commerce-api-extension…363
    tokens truncated…tensions
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/codeforcoders-commerce-api-program.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/codeforcoders-commerce-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/codeforcoders-commerce-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/codeforcoders-commerce-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/financeareaauthorization.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/financeareajwksconfigurationmanager.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/financeareajwtbeareroptionssetup.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/components/financeareatokenoptions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-commerce-api/container.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/account.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/apimodels-staffinvitationmessagesv1.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/apimodels-staffmembermessagesv1.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/auditidentityreferenceendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/auditidentityreferencesv1.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-apimodels-platformheartbeatresponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-endpoints-platformendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-endpoints-staffinvitationendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-endpoints-staffmemberendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-endpoints-staffpasswordrecoveryendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-endpoints-staffpasswordresetendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-endpoints-studentconfirmationendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-endpoints-studentpasswordchangeendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-endpoints-studentpasswordrecoveryendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-exceptionhandlers-globalexceptionhandler.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-extensions-endpointextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-extensions-errorhandlingextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-extensions-healthextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-extensions-observabilityextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-extensions-pipelineextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-extensions-serviceconfigurationextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/codeforcoders-identity-api-program.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/jsonwebkeysetresponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/openidconfigurationendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/openidconfigurationv1.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/serviceassertionextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/serviceassertionoptions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/serviceassertionscopes.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/serviceassertionverifier.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/signingkeyendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/staffprovisioningcommand.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/staffsessionendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/staffsessiontokenissuer.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/staffsessiontokenoptions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/studentaccountendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/studentsessionendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/studentsessiontokenissuer.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/studentsessiontokenoptions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/usertokensigningkeyset.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/components/verifiedserviceassertion.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-identity-api/container.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/components/codeforcoders-learning-api-apimodels-platformheartbeatresponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/components/codeforcoders-learning-api-endpoints-platformendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/components/codeforcoders-learning-api-exceptionhandlers-globalexceptionhandler.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/components/codeforcoders-learning-api-extensions-endpointextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/components/codeforcoders-learning-api-extensions-errorhandlingextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/components/codeforcoders-learning-api-extensions-healthextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/components/codeforcoders-learning-api-extensi…201
    tokens truncated…s
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/components/codeforcoders-learning-api-program.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/components/codeforcoders-learning-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/components/codeforcoders-learning-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/components/codeforcoders-learning-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-learning-api/container.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/codeforcoders-media-api-apimodels-platformheartbeatresponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/codeforcoders-media-api-endpoints-platformendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/codeforcoders-media-api-endpoints-videouploadendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/codeforcoders-media-api-exceptionhandlers-globalexceptionhandler.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/codeforcoders-media-api-extensions-endpointextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/codeforcoders-media-api-extensions-errorhandlingextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/codeforcoders-media-api-extensions-healthextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/codeforcoders-media-api-program.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/codeforcoders-media-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/codeforcoders-media-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/codeforcoders-media-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/mediaauthorization.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/mediajwksconfigurationmanager.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/mediajwtbeareroptionssetup.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/mediatokenoptions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/components/tenantcontextmiddleware.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-media-api/container.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/codeforcoders-notification-api-apimodels-platformheartbeatresponse.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/codeforcoders-notification-api-endpoints-platformendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/codeforcoders-notification-api-exceptionhandlers-globalexceptionhandler.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/codeforcoders-notification-api-extensions-endpointextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/codeforcoders-notification-api-extensions-errorhandlingextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/codeforcoders-notification-api-extensions-healthextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/codeforcoders-notification-api-program.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/codeforcoders-notification-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/codeforcoders-notification-infra-data-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/codeforcoders-notification-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/messagehandlerextensions.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/notificationsendrequestedmessagehandler.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/components/transactionalemaildeliverymessagehandler.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoders-notification-api/container.md
  - >-
    .groma/systems/code-for-coders/containers/codeforcoder…250 tokens
    truncated…ed: codeforcoders-notification-api-extensions-pipelineextensions
  - .groma/systems/code-for-coders/containers/identity/components/account.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/apimodels-staffinvitationmessagesv1.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/apimodels-staffmembermessagesv1.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/auditidentityreferenceendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/auditidentityreferencesv1.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-apimodels-platformheartbeatresponse.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-endpoints-platformendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-endpoints-staffinvitationendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-endpoints-staffmemberendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-endpoints-staffpasswordrecoveryendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-endpoints-staffpasswordresetendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-endpoints-studentconfirmationendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-endpoints-studentpasswordchangeendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-endpoints-studentpasswordrecoveryendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-exceptionhandlers-globalexceptionhandler.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-extensions-endpointextensions.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-extensions-errorhandlingextensions.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-extensions-healthextensions.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-extensions-observabilityextensions.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-extensions-pipelineextensions.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-extensions-serviceconfigurationextensions.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-api-program.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-application-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/codeforcoders-identity-infra-messaging-dependencyinjection.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/identitydbcontext.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/jsonwebkeysetresponse.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/openidconfigurationendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/openidconfigurationv1.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/resolveauditidentityreferences.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/serviceassertionextensions.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/serviceassertionoptions.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/serviceassertionscopes.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/serviceassertionverifier.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/signingkeyendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/staffprovisioningcommand.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/staffsessionendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/staffsessiontokenissuer.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/staffsessiontokenoptions.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/studentaccountendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/studentsessionendpoints.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/studentsessiontokenissuer.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/studentsessiontokenoptions.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/usertokensigningkeyset.md
  - >-
    .groma/systems/code-for-coders/containers/identity/components/verifiedserviceassertion.md
  - .groma/systems/code-for-coders/containers/identity/container.md
  - .groma/systems/code-for-coders/containers/student-spa/components/alert.md
  - .groma/systems/code-for-coders/containers/student-spa/components/avatar.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/brand-logo.md
  - .groma/systems/code-for-coders/containers/student-spa/components/button.md
  - .groma/systems/code-for-coders/containers/student-spa/components/card.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/change-student-password.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/code-window.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/dropdown-menu.md
  - .groma/systems/code-for-coders/containers/student-spa/components/form.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/get-student-workspace-status.md
  - .groma/systems/code-for-coders/containers/student-spa/components/input.md
  - .groma/systems/code-for-coders/containers/student-spa/components/label.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/password-field.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/password-policy-schema.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/register-student.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/request-student-password-reset.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/reset-student-password.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/root-route.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/separator.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/session-markers.md
  - .groma/systems/code-for-coders/containers/student-spa/components/sheet.md
  - .groma/systems/code-for-coders/containers/student-spa/components/sidebar.md
  - .groma/systems/code-for-coders/containers/student-spa/components/skeleton.md
  - .groma/systems/code-for-coders/containers/student-spa/components/sonner.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/status-tile.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-app-layout-route.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-confirmation-route.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-confirmation-screen.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-confirmation.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-dashboard-components-dashboard-screen.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-login-route.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-login-screen.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-password-change-route.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-password-change-screen.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-password-recovery-route.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-password-recovery-screen.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-password-schema.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-registration-route.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-registration-screen.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-session-panel.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-session.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-public-runtime-env-template.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-src-app-app.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-src-app-providers.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-src-app-router.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-src-app-routes-dashboard-route.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-src-app-routes-route-error.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-src-components-app-shell.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-src-components-auth-layout.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-src-config-env.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-src-config-paths.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-src-stores-use-shell-store.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/student-spa-src-utils-get-error-message.md
  - .groma/systems/code-for-coders/containers/student-spa/components/tooltip.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/use-student-confirmation-request-form.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/use-student-login-form.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/use-student-password-change-form.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/use-student-password-reset-form.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/use-student-password-reset-request-form.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/use-student-registration-form.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/components/use-student-session-events.md
  - .groma/systems/code-for-coders/containers/student-spa/container.md
  - >-
    .groma/systems/code-for-coders/containers/student-spa/co…531 tokens
    truncated…udent-spa-src-components-app-shell
  - .groma/systems/code-for-coders/system.md
  - >-
    .groma/systems/…394 tokens
    truncated…ers-media-api-apimodels-platformheartbeatresponse ->
    codeforcoders-media-api-program
  - .groma/syste…288 tokens truncated…s-audit-api-extensions-pipelineextensions
type: docs
ordinal: 1000
---

## Description

<!-- SECTION:DESCRIPTION:BEGIN -->
The repository Groma map currently exposes only a bare system record, leaving new contributors without the implemented application boundaries, responsibilities, users, and integrations. The code and deployment docs already provide evidence for a useful map, so curate it from those sources.
<!-- SECTION:DESCRIPTION:END -->

## Acceptance Criteria
<!-- AC:BEGIN -->
- [x] #1 The map explains the system purpose, users, application boundaries, and important integrations.
- [x] #2 Files that implement one responsibility are combined while independently meaningful responsibilities remain separate.
- [x] #3 Descriptions, overviews, relationships, and important flows reflect source and deployment evidence.
- [x] #4 The Backlog modified-file list and architecture references match the Groma changes.
<!-- AC:END -->

## Implementation Plan

<!-- SECTION:PLAN:BEGIN -->
Inspect the current Groma map and scanner findings; verify boundaries against service entry points, composition, dependency clients, and docs; combine files by responsibility; annotate records and observed relationships; update task links after structural operations; rescan and inspect the rendered plain map.
<!-- SECTION:PLAN:END -->

## Implementation Notes

<!-- SECTION:NOTES:BEGIN -->
Verification: `groma view --plain`, focused service and flow views, a final `groma scan` (created 0, refreshed 63), and a scripted Backlog path/reference audit (1081 modified paths, 87 resolving references). `groma lint --count` reports 177 possible duplicate-logic findings; these are code-level findings outside this documentation task.
<!-- SECTION:NOTES:END -->

## Final Summary

<!-- SECTION:FINAL_SUMMARY:BEGIN -->
Curated the Groma architecture from the current .NET and React source: grouped same-responsibility records, documented users, system scope, infrastructure and cross-service interactions, and added student registration and notification delivery flows. Synced TASK-1 modified files and references; all 87 references resolve. A final Groma scan created 0 components. Groma lint still reports 177 possible duplicate-logic findings in source.
<!-- SECTION:FINAL_SUMMARY:END -->
